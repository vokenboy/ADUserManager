using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Pages;

public partial class ProfilePage : Page, IServiceConsumer
{
    private UserService? _adService;
    private ITerminationService? _terminationService;
    private DatabaseService? _databaseService;
    private MainWindow? _mainWindow;
    private CurrentUserProfileService? _currentUserProfileService;
    private ADUserModel? _currentUser;
    private List<GroupMembershipRecord> _currentGroups = new();

    public ProfilePage()
    {
        InitializeComponent();
        Loaded += (_, _) => TranslationSource.Instance.CultureChanged += ApplyLocalization;
        Unloaded += (_, _) => TranslationSource.Instance.CultureChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        if (_currentUser != null)
            ApplyUser(_currentUser);
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService,
        DatabaseService databaseService, MainWindow mainWindow)
    {
        _adService = adService;
        _terminationService = terminationService;
        _databaseService = databaseService;
        _mainWindow = mainWindow;
        _currentUserProfileService = mainWindow.CurrentUserProfileService;
        _ = LoadProfileAsync();
    }

    private async Task LoadProfileAsync(bool forceRefresh = false)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ClearStatus();

            var t = TranslationSource.Instance;
            if (_adService == null)
            {
                ShowUnavailableState(t["Msg_ProfileUnavailable_NoAd"]);
                return;
            }

            if (_currentUserProfileService == null)
            {
                ShowUnavailableState(t["Msg_ProfileUnavailable_NoContext"]);
                return;
            }

            var user = _currentUserProfileService.GetCurrentUser(_adService, forceRefresh);
            if (user == null)
            {
                ShowUnavailableState(
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

            ApplyUser(user);
            ShowLoadedState();

            if (_terminationService == null)
            {
                UpdateEditorState(enabled: false);
                SetStatus(t["Msg_ProfileEditUnavailable"], Brushes.DarkOrange);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load current user profile", ex);
            ShowUnavailableState($"Failed to load your profile: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        await Task.CompletedTask;
    }

    private void ApplyUser(ADUserModel user)
    {
        var t = TranslationSource.Instance;
        var status = !user.IsEnabled ? t["UserStatus_Disabled"] : user.IsLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
        var lastLogonText = user.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var passwordText = user.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var groupCountText = user.Groups.Count == 1 ? t["Value_OneGroup"] : string.Format(t["Value_NGroups"], user.Groups.Count);

        HeroDisplayNameText.Text = string.IsNullOrWhiteSpace(user.DisplayName) ? user.SamAccountName : user.DisplayName;
        HeroIdentityText.Text = BuildIdentityText(user);
        HeroNoteText.Text = $"{FormatValue(user.Department, t["Value_NoDepartment"])} | {FormatValue(user.Title, t["Value_NoTitle"])}";

        ProfileStatusBadgeText.Text = status;
        SummaryLastLogonText.Text = lastLogonText;
        SummaryPasswordText.Text = passwordText;
        SummaryGroupCountText.Text = groupCountText;

        ReadOnlyUsernameText.Text = FormatValue(user.SamAccountName);
        ReadOnlyEmailText.Text = FormatValue(user.Email);
        ReadOnlyStatusText.Text = status;
        ReadOnlyOuText.Text = FormatValue(user.OrganizationalUnit);
        ReadOnlyDnText.Text = FormatValue(user.DistinguishedName);
        ReadOnlyGroupsText.Text = user.Groups.Count == 0 ? t["Value_NoGroupsFound"] : string.Join(", ", user.Groups);

        FirstNameBox.Text = user.FirstName;
        LastNameBox.Text = user.LastName;
        EditableEmailBox.Text = user.Email;
        DepartmentBox.Text = user.Department;
        TitleBox.Text = user.Title;
        DescriptionBox.Text = user.Description;

        ApplyStatusTheme(user);
        UpdateEditorState(enabled: true);
    }

    private void ApplyStatusTheme(ADUserModel user)
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

    private void ShowLoadedState()
    {
        ContentPanel.Visibility = Visibility.Visible;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowUnavailableState(string message)
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        EmptyStateMessage.Text = message;
        UpdateEditorState(enabled: false);
    }

    private void UpdateEditorState(bool enabled)
    {
        FirstNameBox.IsEnabled = enabled;
        LastNameBox.IsEnabled = enabled;
        EditableEmailBox.IsEnabled = enabled;
        DepartmentBox.IsEnabled = enabled;
        TitleBox.IsEnabled = enabled;
        DescriptionBox.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
        CancelButton.IsEnabled = enabled;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        ClearStatus();
        if (_currentUser != null)
        {
            ApplyUser(_currentUser);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var t = TranslationSource.Instance;
        if (_adService == null || _currentUserProfileService == null)
        {
            SetStatus(t["Msg_ProfileUnavailable_NoAd_Short"], Brushes.IndianRed);
            return;
        }

        if (_terminationService == null)
        {
            SetStatus(t["Msg_ProfileEditUnavailable"], Brushes.IndianRed);
            return;
        }

        if (_currentUser == null)
        {
            SetStatus(t["Msg_ProfileUnavailable_NoLoaded"], Brushes.IndianRed);
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ClearStatus();

            var latestUser = _currentUserProfileService.GetCurrentUser(_adService, forceRefresh: true);
            if (latestUser == null)
            {
                SetStatus("Your profile could not be refreshed before saving.", Brushes.IndianRed);
                return;
            }

            if (!string.IsNullOrWhiteSpace(latestUser.DistinguishedName))
            {
                _currentGroups = _terminationService.GetUserGroups(latestUser.DistinguishedName);
            }

            var request = new UpdateUserRequest
            {
                OriginalSamAccountName = latestUser.SamAccountName,
                FirstName = FirstNameBox.Text.Trim(),
                LastName = LastNameBox.Text.Trim(),
                Email = EditableEmailBox.Text.Trim(),
                Department = DepartmentBox.Text.Trim(),
                Title = TitleBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim(),
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
            ApplyUser(updatedUser);
            SetStatus(TranslationSource.Instance["Msg_ProfileSaved"], Brushes.ForestGreen);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Save current user profile", ex);
            SetStatus($"Failed to save your profile: {ex.Message}", Brushes.IndianRed);
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

    private void SetStatus(string message, Brush brush)
    {
        StatusMessage.Text = message;
        StatusMessage.Foreground = brush;
        StatusMessage.Visibility = Visibility.Visible;
    }

    private void ClearStatus()
    {
        StatusMessage.Text = string.Empty;
        StatusMessage.Visibility = Visibility.Collapsed;
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
}
