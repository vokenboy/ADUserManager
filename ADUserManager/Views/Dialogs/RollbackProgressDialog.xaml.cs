using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class RollbackProgressDialog : Window
{
    private readonly TerminationRecord _record;
    private readonly ITerminationService _terminationService;
    private readonly DatabaseService _databaseService;
    private readonly MementoCaretaker _caretaker;

    public ObservableCollection<StepDisplayItem> Steps { get; } = new();

    /// <summary>
    /// True if the rollback completed without any non-DB errors.
    /// Set after ExecuteRollbackAsync finishes. Check this after ShowDialog() returns.
    /// </summary>
    public bool WasSuccessful { get; private set; }

    public RollbackProgressDialog(
        TerminationRecord record,
        ITerminationService terminationService,
        DatabaseService databaseService,
        MementoCaretaker caretaker)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _record = record;
        _terminationService = terminationService;
        _databaseService = databaseService;
        _caretaker = caretaker;

        Title = $"Rollback: {record.DisplayName}";
        InfoText.Text = $"User: {record.DisplayName} ({record.SamAccountName})\n" +
                        $"Terminated: {record.TerminatedAt:yyyy-MM-dd HH:mm}\n" +
                        $"Performed by: {record.PerformedBy}";

        StepsList.ItemsSource = Steps;
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        await ExecuteRollbackAsync();
    }

    private async Task ExecuteRollbackAsync()
    {
        var steps = new List<(string name, Func<Task> action)>();

        var memento = _record.Memento ?? _caretaker.GetLatest(_record.SamAccountName);

        if (memento != null)
        {
            AppendLog($"[Memento] Using state snapshot from {memento.CapturedAt:yyyy-MM-dd HH:mm:ss}");
            AppendLog($"[Memento] Captured by: {memento.CapturedBy}");

            steps.Add(("Restore state from Memento", async () =>
            {
                await Task.Run(() => _terminationService.RestoreFromMemento(memento));

                AppendLog($"  Account enabled: {memento.IsEnabled}");
                AppendLog($"  OU restored: {memento.OrganizationalUnit}");
                AppendLog($"  Groups restored: {memento.GroupMemberships.Count}");
                foreach (var g in memento.GroupMemberships)
                    AppendLog($"    - {g.GroupName}");
                if (memento.AccountExpiration.HasValue)
                    AppendLog($"  Expiration restored: {memento.AccountExpiration.Value:yyyy-MM-dd}");
                else
                    AppendLog("  Expiration cleared (never expires)");
            }));
        }
        else
        {
            AppendLog("[Memento] Snapshot not found, using manual restore");

            string? currentDN = null;
            var samAccountName = _record.SamAccountName;

            steps.Add(("Find user in AD", async () =>
            {
                await Task.Run(() =>
                {
                    currentDN = _terminationService.FindUserDN(samAccountName);
                });

                if (currentDN == null)
                    throw new InvalidOperationException($"User '{samAccountName}' was not found in AD.");

                AppendLog($"  Found: {currentDN}");
            }));

            if (_record.AccountDisabled)
            {
                steps.Add(("Enable account", async () =>
                {
                    await Task.Run(() => _terminationService.EnableUser(currentDN!));
                    AppendLog("  Account enabled successfully");
                }));
            }

            if (_record.MovedToDisabledOU)
            {
                var originalOU = _record.OriginalOU ?? TerminationService.ParseOUFromDN(_record.DistinguishedName);
                if (!string.IsNullOrEmpty(originalOU))
                {
                    steps.Add(($"Move back to: {originalOU}", async () =>
                    {
                        await Task.Run(() => _terminationService.MoveUser(currentDN!, originalOU));
                        var cn = currentDN!.Split(',')[0];
                        currentDN = $"{cn},{originalOU}";
                        AppendLog($"  Moved back to: {originalOU}");
                    }));
                }
            }

            if (_record.RemovedFromGroups && _record.GroupMemberships.Count > 0)
            {
                steps.Add(($"Restore group memberships ({_record.GroupMemberships.Count})", async () =>
                {
                    var restored = 0;
                    var failed = 0;
                    await Task.Run(() =>
                    {
                        foreach (var group in _record.GroupMemberships)
                        {
                            try
                            {
                                using var groupEntry = new System.DirectoryServices.DirectoryEntry($"LDAP://{group.GroupDN}");
                                groupEntry.Properties["member"].Add(currentDN!);
                                groupEntry.CommitChanges();
                                restored++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    });
                    AppendLog($"  Restored: {restored}, failed: {failed}");
                    if (failed > 0)
                        AppendLog("  (Some groups may have been removed from AD)");
                }));
            }

            if (_record.AccountExpirationSet)
            {
                steps.Add(("Clear account expiration", async () =>
                {
                    await Task.Run(() => _terminationService.ClearAccountExpiration(currentDN!));
                    AppendLog("  Expiration cleared (never expires)");
                }));
            }
        }

        steps.Add(("Save to database", async () =>
        {
            await _databaseService.EnsureInitializedAsync();
            if (!_databaseService.IsAvailable)
            {
                AppendLog("  Skipped (DB unavailable)");
                throw new DatabaseUnavailableException("Database unavailable");
            }
            await _databaseService.MarkAsRolledBackAsync(_record.Id);
            AppendLog($"  Record updated in DB (ID: {_record.Id})");
        }));

        foreach (var step in steps)
        {
            Steps.Add(new StepDisplayItem { Name = step.name, Status = "Pending...", Timestamp = "" });
        }

        var allSuccess = true;
        for (int i = 0; i < steps.Count; i++)
        {
            var (name, action) = steps[i];
            Steps[i].Status = "Running...";
            StatusLabel.Text = $"Running: {name}...";
            StepsList.ScrollIntoView(Steps[i]);
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {name}...");

            try
            {
                await action();
                Steps[i].Status = "Done ✓";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (DatabaseUnavailableException)
            {
                Steps[i].Status = "Skipped ⚠";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                allSuccess = false;
                Steps[i].Status = "Error ✗";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");
                AppendLog($"  ERROR: {ex.Message}");

                ErrorLogger.Log("RollbackProgressDialog", ex);
            }
        }

        if (allSuccess)
        {
            _caretaker.RemoveLatest(_record.SamAccountName);
            AppendLog("[Memento] Snapshot removed from storage");
        }

        WasSuccessful = allSuccess;

        StatusLabel.Text = allSuccess
            ? "Rollback completed successfully!"
            : "Rollback completed with errors.";
        StatusLabel.Foreground = allSuccess
            ? Brushes.Green
            : Brushes.Red;
        CloseButton.IsEnabled = true;

        AppendLog("");
        AppendLog(allSuccess
            ? "=== Rollback completed successfully ==="
            : "=== Rollback completed with errors ===");

        try
        {
            var emailService = new ActiveManager.Services.EmailNotificationService();
            await emailService.SendRollbackNotificationAsync(_record, allSuccess);
            if (AppSettings.Instance.Email.Enabled && AppSettings.Instance.Email.NotifyOnRollback)
                AppendLog("[Email] Notification sent.");
        }
        catch
        {
            AppendLog("[Email] Notification failed (non-critical).");
        }
    }

    private void AppendLog(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(text));
            return;
        }

        LogText.AppendText(text + Environment.NewLine);
        LogText.ScrollToEnd();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
