using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Views.Pages;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace ActiveManager.Views;

public partial class MainWindow : Window
{
    private readonly UserService? _adService;
    private readonly ITerminationService? _terminationService;
    private readonly DatabaseService _databaseService;
    private Func<Page, Task>? _pendingNavigationAction;
    public BackupService BackupService { get; private set; } = null!;
    public MementoCaretaker MementoCaretaker { get; } = new();
    public CurrentUserProfileService CurrentUserProfileService { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _databaseService = new DatabaseService();
        BackupService = new BackupService(_databaseService);
        MementoCaretaker.LoadFromFile();

        try
        {
            _adService = new UserService();
            _terminationService = new TerminationService();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("AD connection", ex);
            var t = TranslationSource.Instance;
            System.Windows.MessageBox.Show(
                $"{t["Msg_AdConnectionError_Body"]}:\n{ex.Message}\n\n{t["Msg_AdConnectionError_Suffix"]}",
                t["Msg_AdConnectionError_Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Loaded += async (_, _) =>
        {
            StatusText.Text = TranslationSource.Instance["Status_DbConnecting"];
            StatusText.Foreground = Brushes.Gray;

            await _databaseService.EnsureInitializedAsync();
            UpdateStatusBar();

            RootNavigation.Navigate(typeof(DashboardPage));
        };

        TranslationSource.Instance.CultureChanged += UpdateStatusBar;
        TranslationSource.Instance.CultureChanged += UpdateUserCountLabel;

        RootNavigation.Navigated += OnNavigated;
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (args.Page is IServiceConsumer consumer)
        {
            consumer.SetServices(_adService, _terminationService, _databaseService, this);
        }

        if (args.Page is Page page && _pendingNavigationAction != null)
        {
            var pendingAction = _pendingNavigationAction;
            _pendingNavigationAction = null;
            _ = pendingAction(page);
        }
    }

    public void UpdateStatusBar()
    {
        var t = TranslationSource.Instance;
        var adStatus = _adService != null
            ? $"{t["Status_AdConnectedTo"]}: {_adService.DomainName}"
            : t["Status_AdUnavailable"];

        if (_databaseService.IsAvailable)
        {
            StatusText.Text = $"{adStatus}  |  {t["Status_DbConnected"]}";
            StatusText.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");
        }
        else
        {
            StatusText.Text = $"{adStatus}  |  {t["Status_DbUnavailable"]}";
            StatusText.Foreground = Brushes.DarkOrange;
        }
    }

    private void UpdateUserCountLabel()
    {
        // Re-format the user count text with the updated "Users" translation
        var current = UserCountText.Text;
        // Extract the number if present (format: "Users: N")
        var colonIdx = current.IndexOf(':');
        if (colonIdx >= 0 && int.TryParse(current[(colonIdx + 1)..].Trim(), out var count))
        {
            UserCountText.Text = $"{TranslationSource.Instance["Status_Users"]}: {count}";
        }
    }

    public void UpdateUserCount(int count)
    {
        UserCountText.Text = $"{TranslationSource.Instance["Status_Users"]}: {count}";
    }

    public void NavigateTo(Type pageType)
    {
        RootNavigation.Navigate(pageType);
    }

    public void NavigateToUsersAndOpenAddUser()
    {
        _pendingNavigationAction = page => page is UsersPage usersPage
            ? usersPage.OpenAddUserAsync()
            : Task.CompletedTask;

        RootNavigation.Navigate(typeof(UsersPage));
    }

    public void ReinitializeDatabase()
    {
        _databaseService.Reinitialize();
        BackupService.ResetCache();
        _ = ReinitializeDatabaseAsync();
    }

    private async Task ReinitializeDatabaseAsync()
    {
        await _databaseService.EnsureInitializedAsync();
        UpdateStatusBar();
    }

    protected override void OnClosed(EventArgs e)
    {
        TranslationSource.Instance.CultureChanged -= UpdateStatusBar;
        TranslationSource.Instance.CultureChanged -= UpdateUserCountLabel;
        _adService?.Dispose();
        _terminationService?.Dispose();
        _databaseService.Dispose();
        base.OnClosed(e);
    }
}
