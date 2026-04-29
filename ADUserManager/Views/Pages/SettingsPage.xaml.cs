using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Pages;

public partial class SettingsPage : Page, IServiceConsumer
{
    private UserService? _adService;
    private ITerminationService? _terminationService;
    private DatabaseService? _databaseService;
    private MainWindow? _mainWindow;
    private CurrentUserProfileService? _currentUserProfileService;
    private ADUserModel? _currentUser;
    private List<GroupMembershipRecord> _currentGroups = new();
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TranslationSource.Instance.CultureChanged += ApplyLocalization;
        ApplyLocalization();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        TranslationSource.Instance.CultureChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        var t = TranslationSource.Instance;

        // Profile tab labels
        LabelLastSignIn1.Text = t["Label_LastSignIn"];
        LabelPasswordUpdated1.Text = t["Label_PasswordUpdated"];
        LabelGroupMemberships1.Text = t["Label_GroupMemberships"];
        LabelProtectedDetails.Text = t["Profile_ProtectedAccountDetails"];
        LabelUsername1.Text = t["Label_Username"];
        LabelPrimaryEmail1.Text = t["Label_PrimaryEmail"];
        LabelAccountStatus1.Text = t["Label_AccountStatus"];
        LabelOrgUnit1.Text = t["Label_OrganizationalUnit"];
        LabelDistName1.Text = t["Label_DistinguishedName"];
        LabelProtectedMemberships.Text = t["Profile_ProtectedMemberships"];
        LabelProtectedMembershipsHint.Text = t["Profile_ProtectedMembershipsHint"];
        LabelEditableDetails.Text = t["Profile_EditableDetails"];
        LabelEditableDetailsHint.Text = t["Profile_EditableDetailsHint_Settings"];
        LabelFirstName1.Text = t["Label_FirstName"];
        LabelEmail1.Text = t["Label_Email"];
        LabelDepartment1.Text = t["Label_Department"];
        LabelLastName1.Text = t["Label_LastName"];
        LabelJobTitle1.Text = t["Label_JobTitle"];
        LabelDescription1.Text = t["Label_Description"];
        LabelSaveChanges1.Text = t["Profile_SaveChanges"];
        ProfileSaveButton.Content = t["Button_Save"];
        ProfileResetButton.Content = t["Button_Reset"];
        ProfileUnavailableTitle.Text = t["Profile_Unavailable_Title"];

        // Database tab buttons
        SaveButton.Content = t["Button_Save"];
        TestButton.Content = t["Button_TestConnection"];
        ReconnectButton.Content = t["Button_Reconnect"];

        // Email tab buttons
        EmailSaveButton.Content = t["Button_Save"];
        EmailTestButton.Content = t["Button_SendTestEmail"];

        // Refresh the DB status badge text
        RefreshDbStatusBadge();
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService,
        DatabaseService databaseService, MainWindow mainWindow)
    {
        _adService = adService;
        _terminationService = terminationService;
        _databaseService = databaseService;
        _mainWindow = mainWindow;
        _currentUserProfileService = mainWindow.CurrentUserProfileService;

        LoadSettings();
        RefreshDbStatusBadge();
        _ = LoadProfileAsync();
    }

    private void LoadSettings()
    {
        _loading = true;
        var db = AppSettings.Instance.Database;

        DbServerBox.Text = db.Server;
        DbPortBox.Text = db.Port.ToString();
        DbNameBox.Text = db.Database;
        DbUserBox.Text = db.User;
        DbPasswordBox.Password = db.Password;
        DbConnectionStringBox.Text = db.RawConnectionString;
        DbCompanyNameBox.Text = db.CompanyName;
        DbDomainNameBox.Text = db.DomainName;

        var email = AppSettings.Instance.Email;
        EmailEnabledBox.IsChecked = email.Enabled;
        SmtpServerBox.Text = email.SmtpServer;
        SmtpPortBox.Text = email.SmtpPort.ToString();
        SmtpSslBox.IsChecked = email.UseSsl;
        SmtpSenderBox.Text = email.SenderAddress;
        SmtpSenderNameBox.Text = email.SenderName;
        SmtpUsernameBox.Text = email.Username;
        SmtpPasswordBox.Password = email.DecryptPassword();
        SmtpRecipientsBox.Text = email.Recipients;
        NotifyTerminationBox.IsChecked = email.NotifyOnTermination;
        NotifyRollbackBox.IsChecked = email.NotifyOnRollback;
        NotifyStepFailureBox.IsChecked = email.NotifyOnStepFailure;

        InitializeLanguageSelector();

        SaveButton.IsEnabled = false;
        EmailSaveButton.IsEnabled = false;
        _loading = false;
    }

    private void InitializeLanguageSelector()
    {
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "Lietuvių", Tag = "lt" });

        var current = AppSettings.Instance.Language;
        LanguageCombo.SelectedIndex = current == "lt" ? 1 : 0;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not ComboBoxItem item) return;

        var lang = item.Tag?.ToString() ?? "en";
        AppSettings.Instance.Language = lang;
        AppSettings.Instance.Save();

        TranslationSource.Instance.CurrentCulture = new CultureInfo(lang);
    }

    private async Task LoadProfileAsync(bool forceRefresh = false)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ClearProfileStatus();

            if (_adService == null)
            {
                ShowProfileUnavailable(TranslationSource.Instance["Msg_ProfileUnavailable_NoAd"]);
                return;
            }

            if (_currentUserProfileService == null)
            {
                ShowProfileUnavailable(TranslationSource.Instance["Msg_ProfileUnavailable_NoContext"]);
                return;
            }

            var user = _currentUserProfileService.GetCurrentUser(_adService, forceRefresh);
            if (user == null)
            {
                ShowProfileUnavailable(
                    $"Your profile could not be matched to Active Directory. Signed-in user: {_currentUserProfileService.WindowsUserName}");
                return;
            }

            _currentUser = user;
            _currentGroups = new List<GroupMembershipRecord>();

            if (_terminationService != null && !string.IsNullOrWhiteSpace(user.DistinguishedName))
            {
                _currentGroups = _terminationService.GetUserGroups(user.DistinguishedName);
                user.Groups = _currentGroups
                    .Select(group => group.GroupName)
                    .Where(group => !string.IsNullOrWhiteSpace(group))
                    .OrderBy(group => group)
                    .ToList();
            }

            ApplyProfile(user);
            ShowProfileLoaded();

            if (_terminationService == null)
            {
                UpdateProfileEditorState(enabled: false);
                SetProfileStatus(TranslationSource.Instance["Msg_ProfileEditUnavailable"], Brushes.DarkOrange);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load current user profile", ex);
            ShowProfileUnavailable($"Failed to load your profile: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        await Task.CompletedTask;
    }

    private void ApplyProfile(ADUserModel user)
    {
        var t = TranslationSource.Instance;
        var status = !user.IsEnabled ? t["UserStatus_Disabled"] : user.IsLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
        var lastLogonText = user.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var passwordText = user.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var groupCountText = user.Groups.Count == 1 ? t["Value_OneGroup"] : $"{user.Groups.Count} {t["Label_GroupMemberships"].ToLower()}";

        ProfileHeroNameText.Text = string.IsNullOrWhiteSpace(user.DisplayName) ? user.SamAccountName : user.DisplayName;
        ProfileHeroIdentityText.Text = BuildIdentityText(user);
        ProfileHeroNoteText.Text = $"{FormatValue(user.Department, t["Value_NoDepartment"])} | {FormatValue(user.Title, t["Value_NoTitle"])}";

        ProfileStatusBadgeText.Text = status;
        ProfileSummaryLastLogonText.Text = lastLogonText;
        ProfileSummaryPasswordText.Text = passwordText;
        ProfileSummaryGroupsText.Text = groupCountText;

        ProfileUsernameText.Text = FormatValue(user.SamAccountName);
        ProfileEmailText.Text = FormatValue(user.Email);
        ProfileStatusText.Text = status;
        ProfileOuText.Text = FormatValue(user.OrganizationalUnit);
        ProfileDnText.Text = FormatValue(user.DistinguishedName);
        ProfileGroupsText.Text = user.Groups.Count == 0 ? t["Value_NoGroupsFound"] : string.Join(", ", user.Groups);

        ProfileFirstNameBox.Text = user.FirstName;
        ProfileLastNameBox.Text = user.LastName;
        ProfileEditableEmailBox.Text = user.Email;
        ProfileDepartmentBox.Text = user.Department;
        ProfileTitleBox.Text = user.Title;
        ProfileDescriptionBox.Text = user.Description;

        ApplyProfileStatusTheme(user);
        UpdateProfileEditorState(enabled: true);
    }

    private void ApplyProfileStatusTheme(ADUserModel user)
    {
        var fillColor = Color.FromRgb(229, 244, 234);
        var borderColor = Color.FromRgb(149, 201, 164);
        var textColor = Color.FromRgb(27, 108, 52);

        if (!user.IsEnabled)
        {
            fillColor = Color.FromRgb(250, 232, 232);
            borderColor = Color.FromRgb(229, 170, 170);
            textColor = Color.FromRgb(155, 40, 40);
        }
        else if (user.IsLockedOut)
        {
            fillColor = Color.FromRgb(255, 242, 223);
            borderColor = Color.FromRgb(233, 193, 130);
            textColor = Color.FromRgb(150, 97, 12);
        }

        ProfileStatusBadgeBorder.Background = new SolidColorBrush(fillColor);
        ProfileStatusBadgeBorder.BorderBrush = new SolidColorBrush(borderColor);
        ProfileStatusBadgeText.Foreground = new SolidColorBrush(textColor);
    }

    private void ShowProfileLoaded()
    {
        ProfileContentPanel.Visibility = Visibility.Visible;
        ProfileEmptyStatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowProfileUnavailable(string message)
    {
        ProfileContentPanel.Visibility = Visibility.Collapsed;
        ProfileEmptyStatePanel.Visibility = Visibility.Visible;
        ProfileEmptyStateMessage.Text = message;
        UpdateProfileEditorState(enabled: false);
    }

    private void UpdateProfileEditorState(bool enabled)
    {
        ProfileFirstNameBox.IsEnabled = enabled;
        ProfileLastNameBox.IsEnabled = enabled;
        ProfileEditableEmailBox.IsEnabled = enabled;
        ProfileDepartmentBox.IsEnabled = enabled;
        ProfileTitleBox.IsEnabled = enabled;
        ProfileDescriptionBox.IsEnabled = enabled;
        ProfileSaveButton.IsEnabled = enabled;
        ProfileResetButton.IsEnabled = enabled;
    }

    private void RefreshDbStatusBadge()
    {
        if (_databaseService == null) return;
        var t = TranslationSource.Instance;

        if (_databaseService.IsAvailable)
        {
            DbStatusBadgeText.Text = t["Status_DbConnected"];
            DbStatusBadge.Background = new SolidColorBrush(Color.FromRgb(34, 139, 34));
            DbStatusBadgeText.Foreground = Brushes.White;
        }
        else
        {
            DbStatusBadgeText.Text = t["Status_DbUnavailable"];
            DbStatusBadge.Background = new SolidColorBrush(Color.FromRgb(180, 60, 0));
            DbStatusBadgeText.Foreground = Brushes.White;
        }
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SaveButton.IsEnabled = true;
        StatusLabel.Text = "";
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SaveButton.IsEnabled = true;
        StatusLabel.Text = "";
    }

    private void OnConnectionStringBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        SaveButton.IsEnabled = true;
        StatusLabel.Text = "";
    }

    private void OnParseConnectionString(object sender, RoutedEventArgs e)
    {
        var raw = DbConnectionStringBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(raw);

            _loading = true;

            // Parse DataSource — may be "host,port" or "host\instance"
            var dataSource = builder.DataSource ?? string.Empty;
            if (dataSource.Contains(','))
            {
                var parts = dataSource.Split(',');
                DbServerBox.Text = parts[0].Trim();
                DbPortBox.Text = parts[1].Trim();
            }
            else
            {
                DbServerBox.Text = dataSource;
                DbPortBox.Text = "1433";
            }

            DbNameBox.Text = builder.InitialCatalog ?? string.Empty;

            if (builder.IntegratedSecurity)
            {
                DbUserBox.Text = string.Empty;
                DbPasswordBox.Password = string.Empty;
            }
            else
            {
                DbUserBox.Text = builder.UserID ?? string.Empty;
                DbPasswordBox.Password = builder.Password ?? string.Empty;
            }

            // Clear the raw string — fields now take over
            DbConnectionStringBox.Text = string.Empty;

            _loading = false;

            SaveButton.IsEnabled = true;
            StatusLabel.Text = TranslationSource.Instance["Msg_ConnectionStringParsed"];
            StatusLabel.Foreground = Brushes.Green;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Parse error: {ex.Message}";
            StatusLabel.Foreground = Brushes.Red;
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var db = AppSettings.Instance.Database;

        db.Enabled = true;
        db.Server = DbServerBox.Text.Trim();
        db.Password = DbPasswordBox.Password;
        db.User = DbUserBox.Text.Trim();
        db.Database = DbNameBox.Text.Trim();
        db.RawConnectionString = DbConnectionStringBox.Text.Trim();
        db.CompanyName = DbCompanyNameBox.Text.Trim();
        db.DomainName = DbDomainNameBox.Text.Trim();

        if (int.TryParse(DbPortBox.Text, out var port))
            db.Port = port;

        try
        {
            AppSettings.Instance.Save();
            _mainWindow?.ReinitializeDatabase();
            SaveButton.IsEnabled = false;
            StatusLabel.Text = TranslationSource.Instance["Msg_SettingsSaved"];
            StatusLabel.Foreground = Brushes.Green;
            _ = RefreshBadgeAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.Foreground = Brushes.Red;
        }
    }

    private async Task RefreshBadgeAfterDelayAsync()
    {
        await Task.Delay(2000);
        await Dispatcher.InvokeAsync(RefreshDbStatusBadge);
    }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        if (_databaseService == null) return;

        TestButton.IsEnabled = false;
        StatusLabel.Text = TranslationSource.Instance["Msg_Connecting"];
        StatusLabel.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

        var (ok, error) = await _databaseService.TestConnectionAsync();

        StatusLabel.Text = ok
            ? TranslationSource.Instance["Msg_ConnectionSuccessful"]
            : $"{TranslationSource.Instance["Msg_ConnectionFailed"]} {error}";
        StatusLabel.Foreground = ok ? Brushes.Green : Brushes.Red;
        TestButton.IsEnabled = true;
        RefreshDbStatusBadge();
    }

    private async void OnReconnect(object sender, RoutedEventArgs e)
    {
        if (_databaseService == null) return;

        ReconnectButton.IsEnabled = false;
        StatusLabel.Text = TranslationSource.Instance["Msg_Reconnecting"];
        StatusLabel.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

        _mainWindow?.ReinitializeDatabase();
        await Task.Delay(2500);

        RefreshDbStatusBadge();
        StatusLabel.Text = _databaseService.IsAvailable
            ? TranslationSource.Instance["Msg_ConnectedSuccessfully"]
            : TranslationSource.Instance["Msg_ConnectionFailed"];
        StatusLabel.Foreground = _databaseService.IsAvailable ? Brushes.Green : Brushes.Red;
        ReconnectButton.IsEnabled = true;
    }

    private void OnProfileReset(object sender, RoutedEventArgs e)
    {
        ClearProfileStatus();
        if (_currentUser != null)
        {
            ApplyProfile(_currentUser);
        }
    }

    private async void OnProfileSave(object sender, RoutedEventArgs e)
    {
        if (_adService == null || _currentUserProfileService == null)
        {
            SetProfileStatus(TranslationSource.Instance["Msg_ProfileUnavailable_NoAd_Short"], Brushes.IndianRed);
            return;
        }

        if (_terminationService == null)
        {
            SetProfileStatus(TranslationSource.Instance["Msg_ProfileEditUnavailable"], Brushes.IndianRed);
            return;
        }

        if (_currentUser == null)
        {
            SetProfileStatus(TranslationSource.Instance["Msg_ProfileUnavailable_NoLoaded"], Brushes.IndianRed);
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ClearProfileStatus();

            var latestUser = _currentUserProfileService.GetCurrentUser(_adService, forceRefresh: true);
            if (latestUser == null)
            {
                SetProfileStatus("Your profile could not be refreshed before saving.", Brushes.IndianRed);
                return;
            }

            if (!string.IsNullOrWhiteSpace(latestUser.DistinguishedName))
            {
                _currentGroups = _terminationService.GetUserGroups(latestUser.DistinguishedName);
            }

            var request = new UpdateUserRequest
            {
                OriginalSamAccountName = latestUser.SamAccountName,
                FirstName = ProfileFirstNameBox.Text.Trim(),
                LastName = ProfileLastNameBox.Text.Trim(),
                Email = ProfileEditableEmailBox.Text.Trim(),
                Department = ProfileDepartmentBox.Text.Trim(),
                Title = ProfileTitleBox.Text.Trim(),
                Description = ProfileDescriptionBox.Text.Trim(),
                TargetOU = latestUser.OrganizationalUnit ?? string.Empty,
                SelectedGroups = _currentGroups
                    .Select(group => group.GroupDN)
                    .Where(groupDn => !string.IsNullOrWhiteSpace(groupDn))
                    .ToList(),
                Enabled = latestUser.IsEnabled
            };

            var updatedUser = _adService.UpdateUser(request);
            updatedUser.Groups = latestUser.Groups;
            _currentUser = updatedUser;
            _currentUserProfileService.SetCachedUser(updatedUser);
            await LogProfileActionAsync(updatedUser);
            ApplyProfile(updatedUser);
            SetProfileStatus(TranslationSource.Instance["Msg_ProfileSaved"], Brushes.ForestGreen);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Save current user profile", ex);
            SetProfileStatus($"Failed to save your profile: {ex.Message}", Brushes.IndianRed);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async Task LogProfileActionAsync(ADUserModel user)
    {
        if (_databaseService == null)
        {
            return;
        }

        try
        {
            await _databaseService.SaveAdminActionAsync(new AdminActionRecord
            {
                ActionType = "Edit own profile",
                SamAccountName = user.SamAccountName,
                DisplayName = user.DisplayName,
                Details = $"Updated own profile for {user.SamAccountName}"
            });
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Admin action audit: Edit own profile", ex);
        }
    }

    private void SetProfileStatus(string message, Brush brush)
    {
        ProfileStatusMessage.Text = message;
        ProfileStatusMessage.Foreground = brush;
        ProfileStatusMessage.Visibility = Visibility.Visible;
    }

    private void ClearProfileStatus()
    {
        ProfileStatusMessage.Text = string.Empty;
        ProfileStatusMessage.Visibility = Visibility.Collapsed;
    }

    private static string BuildIdentityText(ADUserModel user)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(user.SamAccountName))
        {
            parts.Add(user.SamAccountName);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            parts.Add(user.Email);
        }

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    private static string FormatValue(string? value, string fallback = "-")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void OnEmailSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        EmailSaveButton.IsEnabled = true;
        EmailStatusLabel.Text = "";
    }

    private void OnEmailPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        EmailSaveButton.IsEnabled = true;
        EmailStatusLabel.Text = "";
    }

    private void OnEmailSave(object sender, RoutedEventArgs e)
    {
        var email = AppSettings.Instance.Email;

        email.Enabled = EmailEnabledBox.IsChecked == true;
        email.SmtpServer = SmtpServerBox.Text.Trim();
        email.SmtpPort = int.TryParse(SmtpPortBox.Text, out var port) ? port : 587;
        email.UseSsl = SmtpSslBox.IsChecked == true;
        email.SenderAddress = SmtpSenderBox.Text.Trim();
        email.SenderName = SmtpSenderNameBox.Text.Trim();
        email.Username = SmtpUsernameBox.Text.Trim();
        email.Recipients = SmtpRecipientsBox.Text.Trim();
        email.NotifyOnTermination = NotifyTerminationBox.IsChecked == true;
        email.NotifyOnRollback = NotifyRollbackBox.IsChecked == true;
        email.NotifyOnStepFailure = NotifyStepFailureBox.IsChecked == true;

        var plainPassword = SmtpPasswordBox.Password;
        email.EncryptedPassword = string.IsNullOrEmpty(plainPassword)
            ? string.Empty
            : ActiveManager.Services.EmailSettings.EncryptPassword(plainPassword);

        try
        {
            AppSettings.Instance.Save();
            EmailSaveButton.IsEnabled = false;
            EmailStatusLabel.Text = TranslationSource.Instance["Msg_SettingsSaved"];
            EmailStatusLabel.Foreground = Brushes.Green;
        }
        catch (Exception ex)
        {
            EmailStatusLabel.Text = $"Error: {ex.Message}";
            EmailStatusLabel.Foreground = Brushes.Red;
        }
    }

    private async void OnEmailTest(object sender, RoutedEventArgs e)
    {
        EmailTestButton.IsEnabled = false;
        EmailStatusLabel.Text = TranslationSource.Instance["Msg_Sending"];
        EmailStatusLabel.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

        var service = new ActiveManager.Services.EmailNotificationService();
        var error = await service.SendTestEmailAsync();

        EmailStatusLabel.Text = error == null
            ? TranslationSource.Instance["Msg_TestEmailSent"]
            : $"{TranslationSource.Instance["Msg_TestEmailFailed"]}: {error}";
        EmailStatusLabel.Foreground = error == null ? Brushes.Green : Brushes.Red;
        EmailTestButton.IsEnabled = true;
    }
}
