using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;
using ActiveManager.Views.Dialogs;

namespace ActiveManager.Views.Pages;

public partial class HistoryPage : Page, IServiceConsumer
{
    private ITerminationService? _terminationService;
    private DatabaseService? _databaseService;
    private MainWindow? _mainWindow;
    private List<TerminationRecord> _historyRecords = new();
    private readonly DispatcherTimer _searchDebounceTimer;

    public ObservableCollection<HistoryDisplayItem> Records { get; } = new();

    public HistoryPage()
    {
        InitializeComponent();
        HistoryGrid.ItemsSource = Records;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _ = LoadHistoryAsync();
        };

        Loaded += (_, _) => TranslationSource.Instance.CultureChanged += ApplyLocalization;
        Unloaded += (_, _) => TranslationSource.Instance.CultureChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        var selected = (HistoryGrid.SelectedItem as HistoryDisplayItem)?.Record?.Id;
        Records.Clear();
        foreach (var rec in _historyRecords)
            Records.Add(new HistoryDisplayItem(rec));
        if (selected.HasValue)
        {
            var restored = Records.FirstOrDefault(r => r.Record.Id == selected.Value);
            if (restored != null) HistoryGrid.SelectedItem = restored;
        }
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow)
    {
        _terminationService = terminationService;
        _databaseService = databaseService;
        _mainWindow = mainWindow;

        _ = LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        if (_databaseService == null) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            await _databaseService.EnsureInitializedAsync();
            if (!_databaseService.IsAvailable)
            {
                Records.Clear();
                _historyRecords = new();
                Records.Add(new HistoryDisplayItem(new TerminationRecord
                {
                    DisplayName = TranslationSource.Instance["Msg_DbUnavailable"],
                    SamAccountName = "",
                    DistinguishedName = ""
                }, isDbUnavailable: true));
                return;
            }

            _historyRecords = await _databaseService.GetTerminationRecordsAsync(HistorySearchBox.Text.Trim());
            Records.Clear();
            foreach (var rec in _historyRecords)
            {
                Records.Add(new HistoryDisplayItem(rec));
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load termination history", ex);
            System.Windows.MessageBox.Show($"Error loading history:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _searchDebounceTimer.Stop();
            _ = LoadHistoryAsync();
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        HistorySearchBox.Text = "";
        _ = LoadHistoryAsync();
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not HistoryDisplayItem item || item.IsDbUnavailable) return;
        ShowTerminationDetails(item.Record);
    }

    private async void OnRollback(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HistoryDisplayItem item }) return;

        if (item.Record.RolledBack)
        {
            System.Windows.MessageBox.Show("This termination has already been restored.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (item.Record.DeletedFromDirectory)
        {
            System.Windows.MessageBox.Show("This user was deleted from Active Directory and can no longer be restored from history.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Restore is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var rollbackDialog = new RollbackProgressDialog(
            item.Record, _terminationService, _databaseService!, _mainWindow!.MementoCaretaker);
        rollbackDialog.ShowDialog();

        if (rollbackDialog.WasSuccessful)
        {
            await TryLogAdminActionAsync("Restore user", item.Record.SamAccountName, item.Record.DisplayName, "Restored user from termination history");
        }

        await LoadHistoryAsync();
    }

    private static void ShowTerminationDetails(TerminationRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ID: {record.Id}");
        sb.AppendLine($"Name: {record.DisplayName}");
        sb.AppendLine($"Username: {record.SamAccountName}");
        sb.AppendLine($"DN: {record.DistinguishedName}");
        sb.AppendLine($"Terminated on: {record.TerminatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Performed by: {record.PerformedBy}");
        sb.AppendLine();
        sb.AppendLine("=== Actions completed ===");
        if (record.AccountDisabled) sb.AppendLine("OK Account disabled");
        if (record.MovedToDisabledOU) sb.AppendLine($"OK Moved to: {record.TargetOU}");
        if (record.OriginalOU != null) sb.AppendLine($"   Original location: {record.OriginalOU}");
        if (record.PasswordChanged) sb.AppendLine("OK Password changed");
        if (record.RemovedFromGroups) sb.AppendLine("OK Removed from groups");
        if (record.AccountExpirationSet) sb.AppendLine($"OK Expiration date: {record.ExpirationDate:yyyy-MM-dd}");
        if (record.DataExported) sb.AppendLine($"OK Exported to: {record.ExportPath}");
        sb.AppendLine();

        if (record.GroupMemberships.Count > 0)
        {
            sb.AppendLine($"=== Groups ({record.GroupMemberships.Count}) ===");
            foreach (var g in record.GroupMemberships)
            {
                sb.AppendLine($"  - {g.GroupName}");
            }
            sb.AppendLine();
        }

        if (record.RolledBack)
        {
            sb.AppendLine("=== Restore ===");
            sb.AppendLine($"Restored on: {record.RolledBackAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Restored by: {record.RolledBackBy}");
            sb.AppendLine();
        }

        if (record.DeletedFromDirectory)
        {
            sb.AppendLine("=== Deletion ===");
            sb.AppendLine($"Deleted on: {record.DeletedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Deleted by: {record.DeletedBy}");
            sb.AppendLine();
        }

        if (record.StepResults.Count > 0)
        {
            sb.AppendLine("=== Step results ===");
            foreach (var step in record.StepResults)
            {
                var icon = step.Success ? "OK" : "FAIL";
                sb.AppendLine($"  {icon} {step.StepName} ({step.ExecutedAt:HH:mm:ss})");
                if (!string.IsNullOrEmpty(step.ErrorMessage))
                    sb.AppendLine($"    Error: {step.ErrorMessage}");
            }
        }

        System.Windows.MessageBox.Show(sb.ToString(), $"Termination Details - {record.DisplayName}",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private async Task TryLogAdminActionAsync(string actionType, string samAccountName, string displayName, string details)
    {
        if (_databaseService == null)
            return;

        try
        {
            await _databaseService.SaveAdminActionAsync(new AdminActionRecord
            {
                ActionType = actionType,
                SamAccountName = samAccountName,
                DisplayName = displayName,
                Details = details
            });
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Admin action audit: {actionType}", ex);
        }
    }
}

public class HistoryDisplayItem
{
    public TerminationRecord Record { get; }
    public bool IsDbUnavailable { get; }

    public int Id => Record.Id;
    public string DisplayName => Record.DisplayName;
    public string SamAccountName => Record.SamAccountName;
    public string TerminatedAtText => IsDbUnavailable ? "" : Record.TerminatedAt.ToString("yyyy-MM-dd HH:mm");
    public string PerformedBy => Record.PerformedBy;
    public string StatusText
    {
        get
        {
            var t = TranslationSource.Instance;
            if (IsDbUnavailable) return t["History_StatusUnavailable"];
            if (Record.DeletedFromDirectory) return string.Format(t["History_StatusDeleted"], Record.DeletedAt?.ToString("yyyy-MM-dd") ?? "");
            if (Record.RolledBack) return string.Format(t["History_StatusRestored"], Record.RolledBackAt?.ToString("yyyy-MM-dd") ?? "");
            return t["History_StatusTerminated"];
        }
    }
    public bool CanRollback => !IsDbUnavailable && !Record.RolledBack && !Record.DeletedFromDirectory;

    public string StepsText
    {
        get
        {
            if (IsDbUnavailable) return "";
            var t = TranslationSource.Instance;
            var steps = new List<string>();
            if (Record.AccountDisabled) steps.Add(t["History_StepDisabled"]);
            if (Record.MovedToDisabledOU) steps.Add(t["History_StepMoved"]);
            if (Record.PasswordChanged) steps.Add(t["History_StepPassword"]);
            if (Record.RemovedFromGroups) steps.Add(t["History_StepGroups"]);
            if (Record.AccountExpirationSet) steps.Add(t["History_StepExpiration"]);
            if (Record.DataExported) steps.Add(t["History_StepExport"]);
            return string.Join(", ", steps);
        }
    }

    public HistoryDisplayItem(TerminationRecord record, bool isDbUnavailable = false)
    {
        Record = record;
        IsDbUnavailable = isDbUnavailable;
    }
}
