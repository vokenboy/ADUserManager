using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class TerminationProgressDialog : Window
{
    private readonly ADUserModel _user;
    private readonly ITerminationService _terminationService;
    private readonly DatabaseService _databaseService;
    private readonly BackupService _backupService;

    private readonly bool _disableAccount;
    private readonly bool _moveToDisabledOU;
    private readonly string? _targetOU;
    private readonly bool _changePassword;
    private readonly string? _newPassword;
    private readonly bool _removeFromGroups;
    private readonly bool _setExpiration;
    private readonly DateTime? _expirationDate;
    private readonly bool _exportData;
    private readonly string _exportFormat;
    private readonly string? _exportPath;
    private readonly bool _exportGroups;
    private readonly bool _exportPermissions;
    private readonly string _terminationReason;

    private readonly MementoCaretaker _caretaker;

    public ObservableCollection<StepDisplayItem> Steps { get; } = new();
    public TerminationRecord? Result { get; private set; }

    public TerminationProgressDialog(
        ADUserModel user,
        ITerminationService terminationService,
        DatabaseService databaseService,
        BackupService backupService,
        FireUserDialog options,
        MementoCaretaker caretaker)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _user = user;
        _terminationService = terminationService;
        _databaseService = databaseService;
        _backupService = backupService;
        _caretaker = caretaker;
        _disableAccount = options.DisableAccount;
        _moveToDisabledOU = options.MoveToDisabledOU;
        _targetOU = options.TargetOU;
        _changePassword = options.ChangePassword;
        _newPassword = options.NewPassword;
        _removeFromGroups = options.RemoveFromGroups;
        _setExpiration = options.SetExpiration;
        _expirationDate = options.ExpirationDate;
        _exportData = options.ExportData;
        _exportFormat = options.ExportFormat;
        _exportPath = options.ExportPath;
        _exportGroups = options.ExportGroups;
        _exportPermissions = options.ExportPermissions;
        _terminationReason = options.TerminationReason;

        Title = $"Termination: {user.DisplayName}";
        StepsList.ItemsSource = Steps;

        Loaded += async (_, _) => await ExecuteTerminationAsync();
    }

    private async Task ExecuteTerminationAsync()
    {
        var record = new TerminationRecord
        {
            SamAccountName = _user.SamAccountName,
            DisplayName = _user.DisplayName,
            DistinguishedName = _user.DistinguishedName,
            TerminationReason = _terminationReason,
            OriginalOU = TerminationService.ParseOUFromDN(_user.DistinguishedName)
        };

        var steps = new List<(string name, Func<TerminationRecord, Task> action)>();

        steps.Add(("Save user state (Memento + DB backup)", async rec =>
        {
            UserMemento? memento = null;
            await Task.Run(() =>
            {
                memento = _terminationService.CreateMemento(_user);
            });

            if (memento != null)
            {
                rec.Memento = memento;
                rec.GroupMemberships = memento.GroupMemberships.ToList();
                _caretaker.Save(memento);
                AppendLog($"  State captured: {memento.CapturedAt:HH:mm:ss}");
                AppendLog($"  Found {memento.GroupMemberships.Count} group(s)");
                foreach (var g in memento.GroupMemberships)
                    AppendLog($"    - {g.GroupName}");
                AppendLog($"  Account enabled: {memento.IsEnabled}");
                AppendLog($"  OU: {memento.OrganizationalUnit}");
            }

            try
            {
                var preId = await _backupService.CreateBackupAsync(
                    _user,
                    rec.GroupMemberships,
                    memento?.AccountExpiration,
                    BackupType.Termination,
                    OperationType.PreTermination,
                    notes: "Automatic backup before termination");

                if (preId > 0)
                {
                    rec.PreTerminationBackupId = preId;
                    AppendLog($"  DB backup created (ID: {preId})");
                }
                else
                {
                    AppendLog("  DB backup skipped (DB unavailable or company not configured)");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"  DB backup error (skipped): {ex.Message}");
                ErrorLogger.Log("TerminationProgressDialog - DB pre-backup", ex);
            }
        }));

        if (_exportData && !string.IsNullOrEmpty(_exportPath))
        {
            steps.Add(("Export data", async rec =>
            {
                await Task.Run(() =>
                {
                    _terminationService.ExportUserData(_user, rec.GroupMemberships,
                        _exportFormat, _exportPath, _exportGroups, _exportPermissions);
                });
                rec.DataExported = true;
                rec.ExportPath = _exportPath;
                AppendLog($"  Exported to: {_exportPath} ({_exportFormat})");
            }));
        }

        if (_disableAccount)
        {
            steps.Add(("Disable account", async rec =>
            {
                await Task.Run(() => _terminationService.DisableUser(_user.DistinguishedName));
                rec.AccountDisabled = true;
                AppendLog("  Account disabled successfully");
            }));
        }

        if (_changePassword && !string.IsNullOrEmpty(_newPassword))
        {
            steps.Add(("Change password", async rec =>
            {
                await Task.Run(() => _terminationService.ResetPassword(_user.SamAccountName, _newPassword));
                rec.PasswordChanged = true;
                AppendLog("  Password changed to a random value");
            }));
        }

        if (_removeFromGroups)
        {
            steps.Add(("Remove from all groups", async rec =>
            {
                var groupCount = rec.GroupMemberships.Count;
                await Task.Run(() => _terminationService.RemoveFromAllGroups(_user.DistinguishedName));
                rec.RemovedFromGroups = true;
                AppendLog($"  Removed from {groupCount} group(s)");
            }));
        }

        if (_setExpiration && _expirationDate.HasValue)
        {
            steps.Add(("Set account expiration", async rec =>
            {
                await Task.Run(() => _terminationService.SetAccountExpiration(_user.DistinguishedName, _expirationDate.Value));
                rec.AccountExpirationSet = true;
                rec.ExpirationDate = _expirationDate.Value;
                AppendLog($"  Expiration set to: {_expirationDate.Value:yyyy-MM-dd}");
            }));
        }

        if (_moveToDisabledOU && !string.IsNullOrEmpty(_targetOU))
        {
            steps.Add(("Move to disabled OU", async rec =>
            {
                await Task.Run(() => _terminationService.MoveUser(_user.DistinguishedName, _targetOU));
                rec.MovedToDisabledOU = true;
                rec.TargetOU = _targetOU;
                AppendLog($"  Moved to: {_targetOU}");
            }));
        }

        steps.Add(("Save to database", async rec =>
        {
            await _databaseService.EnsureInitializedAsync();
            if (!_databaseService.IsAvailable)
            {
                AppendLog("  Skipped (DB unavailable)");
                throw new DatabaseUnavailableException("Database unavailable");
            }

            try
            {
                var id = await _databaseService.SaveTerminationRecordAsync(
                    rec,
                    rec.PreTerminationBackupId ?? -1,
                    rec.PostTerminationBackupId ?? -1);

                if (id > 0 && rec.PreTerminationBackupId.HasValue)
                {
                    await _backupService.LinkToTerminationRecordAsync(rec.PreTerminationBackupId.Value, id);
                }

                AppendLog($"  Record saved to DB (ID: {id})");
            }
            catch (DatabaseUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendLog($"  DB error (record not saved): {ex.Message}");
                rec.StepResults.Add(new TerminationStepResult
                {
                    StepName = "DB save",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
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
                await action(record);

                Steps[i].Status = "Done ✓";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");

                record.StepResults.Add(new TerminationStepResult
                {
                    StepName = name,
                    Success = true
                });
            }
            catch (DatabaseUnavailableException)
            {
                Steps[i].Status = "Skipped ⚠";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");

                record.StepResults.Add(new TerminationStepResult
                {
                    StepName = name,
                    Success = false,
                    ErrorMessage = "DB unavailable"
                });
            }
            catch (Exception ex)
            {
                allSuccess = false;
                Steps[i].Status = "Error ✗";
                Steps[i].Timestamp = DateTime.Now.ToString("HH:mm:ss");
                AppendLog($"  ERROR: {ex.Message}");

                ErrorLogger.Log("TerminationProgressDialog", ex);

                record.StepResults.Add(new TerminationStepResult
                {
                    StepName = name,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        Result = record;
        StatusLabel.Text = allSuccess
            ? "Termination completed successfully!"
            : "Termination completed with errors.";
        StatusLabel.Foreground = allSuccess
            ? Brushes.Green
            : Brushes.Red;
        CloseButton.IsEnabled = true;
        RevertButton.Visibility = Visibility.Visible;

        AppendLog("");
        AppendLog(allSuccess
            ? "=== Termination completed successfully ==="
            : "=== Termination completed with errors ===");

        try
        {
            var emailService = new ActiveManager.Services.EmailNotificationService();
            await emailService.SendTerminationNotificationAsync(record);
            if (AppSettings.Instance.Email.Enabled && AppSettings.Instance.Email.NotifyOnTermination)
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

    private void OnRevert(object sender, RoutedEventArgs e)
    {
        if (Result == null) return;

        var rollbackDialog = new RollbackProgressDialog(Result, _terminationService, _databaseService, _caretaker);
        rollbackDialog.Owner = this;
        rollbackDialog.ShowDialog();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

public class StepDisplayItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private string _status = "";
    private string _timestamp = "";

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string Timestamp
    {
        get => _timestamp;
        set
        {
            _timestamp = value;
            OnPropertyChanged(nameof(Timestamp));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
