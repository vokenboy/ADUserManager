using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Pages;

public partial class DashboardPage : Page, IServiceConsumer
{
    private DashboardService? _dashboardService;
    private MainWindow? _mainWindow;
    private readonly ObservableCollection<DashboardAlert> _alerts = new();
    private DashboardSummary? _lastSummary;

    public DashboardPage()
    {
        InitializeComponent();
        AlertsList.ItemsSource = _alerts;
        Loaded += (_, _) => TranslationSource.Instance.CultureChanged += ApplyLocalization;
        Unloaded += (_, _) => TranslationSource.Instance.CultureChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        if (_lastSummary != null)
            ApplySummary(_lastSummary);
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _dashboardService = new DashboardService(adService, databaseService, mainWindow.BackupService, mainWindow.MementoCaretaker);
        _ = LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        if (_dashboardService == null)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var summary = await _dashboardService.GetSummaryAsync();
            _lastSummary = summary;
            ApplySummary(summary);
            _mainWindow?.UpdateUserCount(summary.UserMetrics.TotalUsers);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load dashboard", ex);
            MessageBox.Show($"Failed to load dashboard:\n{ex.Message}", "Dashboard Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ApplySummary(DashboardSummary summary)
    {
        var t = TranslationSource.Instance;
        LastUpdatedText.Text = $"{t["Dashboard_LastUpdated"]}: {summary.RefreshedAt:yyyy-MM-dd HH:mm:ss}";
        AdStatusText.Text = summary.Health.AdConnected ? t["Dashboard_Connected"] : t["Dashboard_Unavailable"];
        DomainText.Text = string.IsNullOrWhiteSpace(summary.Health.DomainName)
            ? t["Dashboard_DomainNotDetected"]
            : string.Format(t["Dashboard_Domain"], summary.Health.DomainName);

        DbStatusText.Text = summary.Health.DbAvailable ? t["Dashboard_Connected"] : t["Dashboard_Unavailable"];
        CompanyText.Text = string.Format(t["Dashboard_Company"], summary.Health.CompanyName);
        RecentBackupsText.Text = summary.RecentBackupCount.ToString();
        RestoreCoverageText.Text = string.Format(t["Dashboard_CoverageSummary"], summary.LocalMementoCount, summary.RecentRestoreCount);
        ErrorCountText.Text = string.Format(t["Dashboard_ErrorsInSession"], summary.Health.ErrorCount);

        TotalUsersText.Text = summary.UserMetrics.TotalUsers.ToString();
        ActiveUsersText.Text = summary.UserMetrics.ActiveUsers.ToString();
        RiskUsersText.Text = $"{summary.UserMetrics.DisabledUsers} / {summary.UserMetrics.LockedUsers}";
        InactiveUsersLabel.Text = string.Format(t["Dashboard_InactiveThreshold"], summary.UserMetrics.InactivityThresholdDays);
        InactiveUsersText.Text = summary.UserMetrics.InactiveUsers.ToString();

        _alerts.Clear();
        if (summary.Alerts.Count == 0)
        {
            _alerts.Add(new DashboardAlert { Severity = t["Dashboard_AlertSeverityOk"], Message = t["Dashboard_NoAlerts"] });
        }
        else
        {
            foreach (var alert in summary.Alerts)
            {
                _alerts.Add(alert);
            }
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => _ = LoadDashboardAsync();

    private void OnOpenUsers(object sender, RoutedEventArgs e) => _mainWindow?.NavigateTo(typeof(UsersPage));

    private void OnOpenAddUser(object sender, RoutedEventArgs e) => _mainWindow?.NavigateToUsersAndOpenAddUser();

    private void OnOpenRestore(object sender, RoutedEventArgs e) => _mainWindow?.NavigateTo(typeof(MementoPage));

    private void OnOpenHistory(object sender, RoutedEventArgs e) => _mainWindow?.NavigateTo(typeof(HistoryPage));

    private void OnOpenErrors(object sender, RoutedEventArgs e) => _mainWindow?.NavigateTo(typeof(ErrorLogPage));
}
