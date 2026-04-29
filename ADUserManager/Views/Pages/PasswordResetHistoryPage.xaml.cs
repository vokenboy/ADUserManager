using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ActiveManager.Helpers;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Pages;

public partial class PasswordResetHistoryPage : Page, IServiceConsumer
{
    private DatabaseService? _databaseService;
    private readonly DispatcherTimer _searchDebounceTimer;

    public ObservableCollection<PasswordResetDisplayItem> Records { get; } = new();

    public PasswordResetHistoryPage()
    {
        InitializeComponent();
        PasswordResetGrid.ItemsSource = Records;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _ = LoadRecordsAsync();
        };
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow)
    {
        _databaseService = databaseService;
        _ = LoadRecordsAsync();
    }

    private async Task LoadRecordsAsync()
    {
        if (_databaseService == null)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await _databaseService.EnsureInitializedAsync();

            Records.Clear();
            if (!_databaseService.IsAvailable)
            {
                return;
            }

            var results = await _databaseService.GetPasswordResetRecordsAsync(SearchBox.Text.Trim());
            foreach (var result in results)
            {
                Records.Add(new PasswordResetDisplayItem(result));
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load password reset history", ex);
            MessageBox.Show($"Error loading password reset history:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => _ = LoadRecordsAsync();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }
}

public class PasswordResetDisplayItem
{
    public string DisplayName { get; }
    public string SamAccountName { get; }
    public string DistinguishedName { get; }
    public string PerformedBy { get; }
    public string ResetAtText { get; }
    public string ForceChangeText { get; }

    public PasswordResetDisplayItem(PasswordResetRecord record)
    {
        DisplayName = record.DisplayName;
        SamAccountName = record.SamAccountName;
        DistinguishedName = record.DistinguishedName;
        PerformedBy = record.PerformedBy;
        ResetAtText = record.ResetAt.ToString("yyyy-MM-dd HH:mm");
        ForceChangeText = record.ForceChangeAtNextSignIn ? "Yes" : "No";
    }
}
