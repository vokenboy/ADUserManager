using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;
using ActiveManager.Views.Dialogs;
using Microsoft.Win32;

namespace ActiveManager.Views.Pages;

public partial class UsersPage : Page, IServiceConsumer
{
    private UserService? _adService;
    private GroupService? _groupService;
    private UserProvisioningService? _userProvisioningService;
    private ITerminationService? _terminationService;
    private DatabaseService? _databaseService;
    private BackupService? _backupService;
    private MainWindow? _mainWindow;
    private CurrentUserProfileService? _currentUserProfileService;
    private List<ADUserModel> _currentUsers = new();
    private List<ADUserModel> _filteredUsers = new();
    private readonly DispatcherTimer _searchDebounceTimer;
    private bool _isUpdatingFilters;

    public ObservableCollection<UserDisplayItem> Users { get; } = new();

    public UsersPage()
    {
        InitializeComponent();
        UsersGrid.ItemsSource = Users;
        InitializeFilterControls();

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            LoadUsers();
        };

        Loaded += (_, _) => TranslationSource.Instance.CultureChanged += ApplyLocalization;
        Unloaded += (_, _) => TranslationSource.Instance.CultureChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        var selectedStatusTag = GetComboTag(StatusFilterComboBox);
        var selectedLastSignInTag = GetComboTag(LastSignInFilterComboBox);
        var selectedPasswordUpdatedTag = GetComboTag(PasswordUpdatedFilterComboBox);
        var selectedOuIndex = OuFilterComboBox.SelectedIndex;

        // Re-populate translatable filter options, preserving selection by tag
        InitializeTranslatableFilters();

        // Restore selections by tag
        RestoreComboByTag(StatusFilterComboBox, selectedStatusTag);
        RestoreComboByTag(LastSignInFilterComboBox, selectedLastSignInTag);
        RestoreComboByTag(PasswordUpdatedFilterComboBox, selectedPasswordUpdatedTag);

        // Re-populate OU filter (first item is a ComboBoxItem sentinel, rest are strings)
        var ouSentinel = OuFilterComboBox.Items.Count > 0 ? OuFilterComboBox.Items[0] as ComboBoxItem : null;
        if (ouSentinel != null)
            ouSentinel.Content = TranslationSource.Instance["Filter_All"];
        OuFilterComboBox.SelectedIndex = selectedOuIndex >= 0 && selectedOuIndex < OuFilterComboBox.Items.Count
            ? selectedOuIndex : 0;

        // Refresh grid rows to update localized status/date text
        ApplyFiltersAndRefreshGrid();

        // Refresh empty state
        UpdateEmptyState();

        // Refresh no-selection panel if visible
        NoSelectionTitleText.Text = TranslationSource.Instance["Users_SelectUser_Title"];
        NoSelectionBodyText.Text = TranslationSource.Instance["Users_SelectUser_Body"];
    }

    private static string GetComboTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag) return tag;
        return string.Empty;
    }

    private static void RestoreComboByTag(ComboBox combo, string tag)
    {
        if (string.IsNullOrEmpty(tag)) { combo.SelectedIndex = 0; return; }
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag as string == tag) { combo.SelectedItem = item; return; }
        }
        combo.SelectedIndex = 0;
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow)
    {
        _adService = adService;
        _terminationService = terminationService;
        _databaseService = databaseService;
        _backupService = mainWindow.BackupService;
        _mainWindow = mainWindow;
        _currentUserProfileService = mainWindow.CurrentUserProfileService;
        AddUserButtonState();

        if (_adService != null)
        {
            try
            {
                _groupService ??= new GroupService();
                _userProvisioningService ??= new UserProvisioningService();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("User creation service startup", ex);
                _groupService = null;
                _userProvisioningService = null;
            }
        }

        if (Users.Count == 0)
            LoadUsers();
    }

    private void AddUserButtonState()
    {
        if (AddUserToolbarButton == null)
            return;

        AddUserToolbarButton.IsEnabled = _adService != null;
        AddUserToolbarButton.ToolTip = _adService != null
            ? "Create a new Active Directory user"
            : "AD unavailable";
        UpdateSelectedUserActionState();
    }

    private void LoadUsers()
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            if (_adService == null)
            {
                _currentUsers = GenerateMockUsers(SearchBox.Text.Trim());
            }
            else
            {
                _currentUsers = _adService.SearchUsers(SearchBox.Text.Trim());
            }

            if (_currentUserProfileService != null)
            {
                _currentUsers = _currentUsers
                    .Where(user => !_currentUserProfileService.IsCurrentUser(user))
                    .ToList();
            }

            RefreshDynamicFilterOptions();
            ApplyFiltersAndRefreshGrid();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load users", ex);
            System.Windows.MessageBox.Show($"Error loading users:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void LoadUsersAndSelect(string samAccountName)
    {
        LoadUsers();

        var target = Users.FirstOrDefault(u =>
            string.Equals(u.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            SearchBox.Text = samAccountName;
            LoadUsers();
            target = Users.FirstOrDefault(u =>
                string.Equals(u.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));
        }

        if (target != null)
        {
            UsersGrid.SelectedItem = target;
            UsersGrid.ScrollIntoView(target);
        }
    }

    private static List<ADUserModel> GenerateMockUsers(string filter)
    {
        var mockUsers = new List<ADUserModel>
        {
            new ADUserModel
            {
                SamAccountName = "j.smith",
                DisplayName = "John Smith",
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@company.com",
                Department = "IT",
                Title = "System Administrator",
                Description = "Senior IT administrator",
                DistinguishedName = "CN=John Smith,OU=IT,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=IT,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-45),
                LastLogon = DateTime.Now.AddHours(-2),
                Groups = new List<string> { "Domain Users", "IT Admins", "Backup Operators" }
            },
            new ADUserModel
            {
                SamAccountName = "a.johnson",
                DisplayName = "Alice Johnson",
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice.johnson@company.com",
                Department = "Finance",
                Title = "Financial Analyst",
                Description = "Senior financial analyst",
                DistinguishedName = "CN=Alice Johnson,OU=Finance,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=Finance,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-30),
                LastLogon = DateTime.Now.AddHours(-5),
                Groups = new List<string> { "Domain Users", "Finance Team", "Report Viewers" }
            },
            new ADUserModel
            {
                SamAccountName = "m.williams",
                DisplayName = "Michael Williams",
                FirstName = "Michael",
                LastName = "Williams",
                Email = "michael.williams@company.com",
                Department = "HR",
                Title = "HR Manager",
                Description = "Human resources manager",
                DistinguishedName = "CN=Michael Williams,OU=HR,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=HR,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-15),
                LastLogon = DateTime.Now.AddMinutes(-30),
                Groups = new List<string> { "Domain Users", "HR Team", "Employee Data Viewers" }
            },
            new ADUserModel
            {
                SamAccountName = "s.brown",
                DisplayName = "Sarah Brown",
                FirstName = "Sarah",
                LastName = "Brown",
                Email = "sarah.brown@company.com",
                Department = "Marketing",
                Title = "Marketing Specialist",
                Description = "Digital marketing specialist",
                DistinguishedName = "CN=Sarah Brown,OU=Marketing,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=Marketing,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-60),
                LastLogon = DateTime.Now.AddDays(-1),
                Groups = new List<string> { "Domain Users", "Marketing Team" }
            },
            new ADUserModel
            {
                SamAccountName = "d.jones",
                DisplayName = "David Jones",
                FirstName = "David",
                LastName = "Jones",
                Email = "david.jones@company.com",
                Department = "IT",
                Title = "Developer",
                Description = "Software developer",
                DistinguishedName = "CN=David Jones,OU=IT,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=IT,OU=Users,DC=company,DC=local",
                IsEnabled = false,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-120),
                LastLogon = DateTime.Now.AddDays(-30),
                Groups = new List<string> { "Domain Users", "Developers" }
            },
            new ADUserModel
            {
                SamAccountName = "e.davis",
                DisplayName = "Emma Davis",
                FirstName = "Emma",
                LastName = "Davis",
                Email = "emma.davis@company.com",
                Department = "Sales",
                Title = "Sales Manager",
                Description = "Regional sales manager",
                DistinguishedName = "CN=Emma Davis,OU=Sales,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=Sales,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-20),
                LastLogon = DateTime.Now.AddHours(-1),
                Groups = new List<string> { "Domain Users", "Sales Team", "Sales Managers", "CRM Users" }
            },
            new ADUserModel
            {
                SamAccountName = "r.wilson",
                DisplayName = "Robert Wilson",
                FirstName = "Robert",
                LastName = "Wilson",
                Email = "robert.wilson@company.com",
                Department = "Operations",
                Title = "Operations Coordinator",
                Description = "Daily operations coordinator",
                DistinguishedName = "CN=Robert Wilson,OU=Operations,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=Operations,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = true,
                PasswordLastSet = DateTime.Now.AddDays(-90),
                LastLogon = DateTime.Now.AddDays(-2),
                Groups = new List<string> { "Domain Users", "Operations Team" }
            },
            new ADUserModel
            {
                SamAccountName = "l.taylor",
                DisplayName = "Laura Taylor",
                FirstName = "Laura",
                LastName = "Taylor",
                Email = "laura.taylor@company.com",
                Department = "IT",
                Title = "Help Desk Technician",
                Description = "IT support technician",
                DistinguishedName = "CN=Laura Taylor,OU=IT,OU=Users,DC=company,DC=local",
                OrganizationalUnit = "OU=IT,OU=Users,DC=company,DC=local",
                IsEnabled = true,
                IsLockedOut = false,
                PasswordLastSet = DateTime.Now.AddDays(-10),
                LastLogon = DateTime.Now.AddMinutes(-15),
                Groups = new List<string> { "Domain Users", "IT Support", "Help Desk" }
            }
        };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var lowerFilter = filter.ToLower();
            mockUsers = mockUsers.Where(u =>
                u.DisplayName.ToLower().Contains(lowerFilter) ||
                u.SamAccountName.ToLower().Contains(lowerFilter) ||
                u.Email.ToLower().Contains(lowerFilter) ||
                u.Department.ToLower().Contains(lowerFilter)
            ).ToList();
        }

        return mockUsers;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _searchDebounceTimer.Stop();
            LoadUsers();
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
        ResetFilters();
        SearchBox.Text = "";
        LoadUsers();
    }

    private void OnUserSelected(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionState();
        UpdateSelectedUserActionState();
    }

    private void UpdateUserDetails(ADUserModel user)
    {
        if (user.Groups.Count == 0 && !string.IsNullOrEmpty(user.DistinguishedName) && _terminationService != null)
        {
            user.Groups = _terminationService.GetUserGroups(user.DistinguishedName)
                .Select(g => g.GroupName)
                .ToList();
        }

        var t = TranslationSource.Instance;
        var status = !user.IsEnabled ? t["UserStatus_Disabled"] : user.IsLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
        var passwordLastSetText = user.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var lastLogonText = user.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        var groupCountText = user.Groups.Count == 1 ? t["Value_OneGroup"] : string.Format(t["Value_NGroups"], user.Groups.Count);

        DetailsHeader.Text = string.IsNullOrWhiteSpace(user.DisplayName) ? user.SamAccountName : user.DisplayName;
        DetailIdentitySubheader.Text = BuildIdentitySubheader(user);
        DetailIdentityNote.Text = BuildIdentityNote(user);

        DetailFirstName.Text = FormatDetailValue(user.FirstName);
        DetailLastName.Text = FormatDetailValue(user.LastName);
        DetailEmail.Text = FormatDetailValue(user.Email);
        DetailDescription.Text = FormatDetailValue(user.Description);
        DetailUsername.Text = user.SamAccountName;
        DetailDepartment.Text = FormatDetailValue(user.Department);
        DetailTitle.Text = FormatDetailValue(user.Title);
        DetailStatus.Text = status;
        DetailPasswordLastSet.Text = passwordLastSetText;
        DetailLastLogon.Text = lastLogonText;
        DetailOU.Text = FormatDetailValue(user.OrganizationalUnit);
        DetailDN.Text = FormatDetailValue(user.DistinguishedName);

        DetailStatusBadgeText.Text = status;
        DetailGroupCountSummary.Text = groupCountText;
        DetailLastLogonSummary.Text = lastLogonText;
        DetailPasswordLastSetSummary.Text = passwordLastSetText;

        ApplyStatusTheme(user);

        DetailGroups.ItemsSource = user.Groups.Count > 0 ? user.Groups : new[] { t["Value_NoGroupsFound"] };
    }

    private static string FormatDetailValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string BuildIdentitySubheader(ADUserModel user)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(user.SamAccountName))
            parts.Add(user.SamAccountName);

        if (!string.IsNullOrWhiteSpace(user.Email))
            parts.Add(user.Email);

        return parts.Count > 0 ? string.Join(" | ", parts) : "-";
    }

    private static string BuildIdentityNote(ADUserModel user)
    {
        var t = TranslationSource.Instance;
        var department = string.IsNullOrWhiteSpace(user.Department) ? t["Value_NoDepartment"] : user.Department;
        var title = string.IsNullOrWhiteSpace(user.Title) ? t["Value_TitleUnavailable"] : user.Title;
        return $"{department} | {title}";
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

        DetailStatusBadgeBorder.Background = new SolidColorBrush(fillColor);
        DetailStatusBadgeBorder.BorderBrush = new SolidColorBrush(borderColor);
        DetailStatusBadgeText.Foreground = new SolidColorBrush(textColor);
    }

    private void InitializeFilterControls()
    {
        _isUpdatingFilters = true;
        InitializeTranslatableFilters();
        OuFilterComboBox.Items.Clear();
        OuFilterComboBox.Items.Add(new ComboBoxItem { Content = TranslationSource.Instance["Filter_All"], Tag = "ALL_OUS" });
        OuFilterComboBox.SelectedIndex = 0;
        _isUpdatingFilters = false;
    }

    private void InitializeTranslatableFilters()
    {
        _isUpdatingFilters = true;
        var t = TranslationSource.Instance;

        StatusFilterComboBox.Items.Clear();
        StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_All"], Tag = "all" });
        StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Active"], Tag = "active" });
        StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Disabled"], Tag = "disabled" });
        StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Locked"], Tag = "locked" });
        StatusFilterComboBox.SelectedIndex = 0;

        LastSignInFilterComboBox.Items.Clear();
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_AnyTime"], Tag = "anytime" });
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Value_Never"], Tag = "never" });
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = "Today", Tag = "today" });
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Last7Days"], Tag = "last7" });
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Last30Days"], Tag = "last30" });
        LastSignInFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Over90Days"], Tag = "older30" });
        LastSignInFilterComboBox.SelectedIndex = 0;

        PasswordUpdatedFilterComboBox.Items.Clear();
        PasswordUpdatedFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_AnyTime"], Tag = "anytime" });
        PasswordUpdatedFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Value_Never"], Tag = "never" });
        PasswordUpdatedFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Last30Days"], Tag = "last30" });
        PasswordUpdatedFilterComboBox.Items.Add(new ComboBoxItem { Content = "31-90 " + t["Filter_Last30Days"].ToLower(), Tag = "31to90" });
        PasswordUpdatedFilterComboBox.Items.Add(new ComboBoxItem { Content = t["Filter_Over90Days"], Tag = "older90" });
        PasswordUpdatedFilterComboBox.SelectedIndex = 0;

        _isUpdatingFilters = false;
    }

    private void RefreshDynamicFilterOptions()
    {
        _isUpdatingFilters = true;

        var selectedOu = OuFilterComboBox.SelectedItem as string;

        var ous = _currentUsers
            .Select(user => user.OrganizationalUnit?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        OuFilterComboBox.Items.Clear();
        OuFilterComboBox.Items.Add(new ComboBoxItem { Content = TranslationSource.Instance["Filter_All"], Tag = "ALL_OUS" });
        foreach (var ou in ous)
            OuFilterComboBox.Items.Add(ou);

        if (!string.IsNullOrWhiteSpace(selectedOu) && ous.Contains(selectedOu, StringComparer.OrdinalIgnoreCase))
            OuFilterComboBox.SelectedItem = selectedOu;
        else
            OuFilterComboBox.SelectedIndex = 0;

        _isUpdatingFilters = false;
    }

    private void ApplyFiltersAndRefreshGrid()
    {
        var selectedSamAccountName = (UsersGrid.SelectedItem as UserDisplayItem)?.SamAccountName;
        _filteredUsers = _currentUsers.Where(MatchesCurrentFilters).ToList();

        Users.Clear();
        foreach (var user in _filteredUsers)
        {
            Users.Add(new UserDisplayItem(user));
        }

        _mainWindow?.UpdateUserCount(_filteredUsers.Count);

        var restoredSelection = !string.IsNullOrWhiteSpace(selectedSamAccountName)
            ? Users.FirstOrDefault(user => string.Equals(user.SamAccountName, selectedSamAccountName, StringComparison.OrdinalIgnoreCase))
            : null;

        UsersGrid.SelectedItem = restoredSelection;

        if (restoredSelection != null)
        {
            UsersGrid.ScrollIntoView(restoredSelection);
        }

        UpdateEmptyState();
        UpdateSelectionState();
        UpdateSelectedUserActionState();
    }

    private bool MatchesCurrentFilters(ADUserModel user)
    {
        var selectedStatusTag = GetComboTag(StatusFilterComboBox);
        if (!MatchesStatusFilter(user, selectedStatusTag))
            return false;

        // OU filter: index 0 is "all" sentinel, items 1+ are OU strings
        if (OuFilterComboBox.SelectedIndex > 0 && OuFilterComboBox.SelectedItem is string selectedOu)
        {
            if (!string.Equals(user.OrganizationalUnit, selectedOu, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!MatchesLastSignInFilter(user, GetComboTag(LastSignInFilterComboBox)))
            return false;

        if (!MatchesPasswordUpdatedFilter(user, GetComboTag(PasswordUpdatedFilterComboBox)))
            return false;

        return true;
    }

    private static bool MatchesStatusFilter(ADUserModel user, string? tag)
    {
        return tag switch
        {
            "active" => user.IsEnabled && !user.IsLockedOut,
            "disabled" => !user.IsEnabled,
            "locked" => user.IsEnabled && user.IsLockedOut,
            _ => true
        };
    }

    private static bool MatchesLastSignInFilter(ADUserModel user, string? tag)
    {
        var now = DateTime.Now;

        return tag switch
        {
            "never" => user.LastLogon == null,
            "today" => user.LastLogon?.Date == now.Date,
            "last7" => user.LastLogon != null && user.LastLogon >= now.AddDays(-7),
            "last30" => user.LastLogon != null && user.LastLogon >= now.AddDays(-30),
            "older30" => user.LastLogon != null && user.LastLogon < now.AddDays(-30),
            _ => true
        };
    }

    private static bool MatchesPasswordUpdatedFilter(ADUserModel user, string? tag)
    {
        var now = DateTime.Now;

        return tag switch
        {
            "never" => user.PasswordLastSet == null,
            "last30" => user.PasswordLastSet != null && user.PasswordLastSet >= now.AddDays(-30),
            "31to90" => user.PasswordLastSet != null &&
                user.PasswordLastSet < now.AddDays(-30) &&
                user.PasswordLastSet >= now.AddDays(-90),
            "older90" => user.PasswordLastSet != null && user.PasswordLastSet < now.AddDays(-90),
            _ => true
        };
    }

    private int GetActiveFilterCount()
    {
        var count = 0;
        var statusTag = GetComboTag(StatusFilterComboBox);
        if (!string.IsNullOrEmpty(statusTag) && statusTag != "all") count++;
        if (OuFilterComboBox.SelectedIndex > 0) count++;
        var lastSignInTag = GetComboTag(LastSignInFilterComboBox);
        if (!string.IsNullOrEmpty(lastSignInTag) && lastSignInTag != "anytime") count++;
        var passwordTag = GetComboTag(PasswordUpdatedFilterComboBox);
        if (!string.IsNullOrEmpty(passwordTag) && passwordTag != "anytime") count++;
        return count;
    }

    private void UpdateEmptyState()
    {
        var t = TranslationSource.Instance;
        if (_filteredUsers.Count == 0)
        {
            UserDetailsPanel.Visibility = Visibility.Collapsed;
            NoSelectionMessage.Visibility = Visibility.Visible;
            NoSelectionTitleText.Text = GetActiveFilterCount() > 0
                ? t["Users_NoMatch_Title"]
                : t["Users_NoUsers_Title"];
            NoSelectionBodyText.Text = GetActiveFilterCount() > 0
                ? t["Users_NoMatch_Body"]
                : t["Users_NoUsers_Body"];
            return;
        }

        NoSelectionTitleText.Text = t["Users_SelectUser_Title"];
        NoSelectionBodyText.Text = t["Users_SelectUser_Body"];
    }

    private void UpdateSelectionState()
    {
        if (_filteredUsers.Count == 0)
        {
            UserDetailsPanel.Visibility = Visibility.Collapsed;
            NoSelectionMessage.Visibility = Visibility.Visible;
            return;
        }

        if (UsersGrid.SelectedItem is UserDisplayItem selectedUser)
        {
            UpdateUserDetails(selectedUser.Model);
            UserDetailsPanel.Visibility = Visibility.Visible;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            return;
        }

        UserDetailsPanel.Visibility = Visibility.Collapsed;
        NoSelectionMessage.Visibility = Visibility.Visible;
    }

    private void ResetFilters()
    {
        _isUpdatingFilters = true;
        StatusFilterComboBox.SelectedIndex = 0;
        OuFilterComboBox.SelectedIndex = 0;
        LastSignInFilterComboBox.SelectedIndex = 0;
        PasswordUpdatedFilterComboBox.SelectedIndex = 0;
        _isUpdatingFilters = false;
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFilters)
            return;

        ApplyFiltersAndRefreshGrid();
    }

    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        ResetFilters();
        ApplyFiltersAndRefreshGrid();
    }

    private void OnExportCsv(object sender, RoutedEventArgs e)
    {
        if (_filteredUsers.Count == 0)
        {
            System.Windows.MessageBox.Show("There are no users to export.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"AD_Users_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Username,Display Name,First Name,Last Name,Email,Department,Job Title,Status,Last Sign-In,Password Last Changed");
            foreach (var u in _filteredUsers)
            {
                var status = !u.IsEnabled ? "Disabled" : u.IsLockedOut ? "Locked" : "Active";
                sb.AppendLine($"\"{u.SamAccountName}\",\"{u.DisplayName}\",\"{u.FirstName}\",\"{u.LastName}\",\"{u.Email}\",\"{u.Department}\",\"{u.Title}\",\"{status}\",\"{u.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? ""}\",\"{u.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? ""}\"");
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            System.Windows.MessageBox.Show($"Exported {_filteredUsers.Count} users to:\n{dialog.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("CSV export", ex);
            System.Windows.MessageBox.Show($"Export error:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRevertFromFile(object sender, RoutedEventArgs e)
    {
        if (_terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Restore is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Export files (*.json;*.csv;*.xml)|*.json;*.csv;*.xml|All files (*.*)|*.*",
            Title = "Select an export file to restore"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var record = ParseExportFile(dialog.FileName);
            if (record == null)
            {
                System.Windows.MessageBox.Show("Failed to read the export file.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            record.ExportPath = dialog.FileName;
            record.DataExported = true;

            var confirm = System.Windows.MessageBox.Show(
                $"Restore user: {record.DisplayName} ({record.SamAccountName})?\n\n" +
                $"Groups: {record.GroupMemberships.Count}\n" +
                $"Original location: {record.OriginalOU ?? "unknown"}",
                "Restore From File",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var rollbackDialog = new RollbackProgressDialog(record, _terminationService, _databaseService!, _mainWindow!.MementoCaretaker);
            rollbackDialog.ShowDialog();

            if (rollbackDialog.WasSuccessful)
            {
                await TryLogAdminActionAsync("Restore user", new ADUserModel
                {
                    SamAccountName = record.SamAccountName,
                    DisplayName = record.DisplayName,
                    DistinguishedName = record.DistinguishedName
                }, $"Restored user {record.SamAccountName} from export file");
            }

            LoadUsers();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Restore from file", ex);
            System.Windows.MessageBox.Show($"Error reading file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static TerminationRecord? ParseExportFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => ParseJsonExport(filePath),
            ".csv" => ParseCsvExport(filePath),
            ".xml" => ParseXmlExport(filePath),
            _ => null
        };
    }

    private static TerminationRecord? ParseJsonExport(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("VartotojoInfo", out var info))
            return null;

        var record = new TerminationRecord
        {
            SamAccountName = info.TryGetProperty("SamAccountName", out var sam) ? sam.GetString() ?? "" : "",
            DisplayName = info.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "",
            DistinguishedName = info.TryGetProperty("DistinguishedName", out var ddn) ? ddn.GetString() ?? "" : "",
            OriginalOU = info.TryGetProperty("OrganizationalUnit", out var ou) ? ou.GetString() : null,
            AccountDisabled = true,
            MovedToDisabledOU = true,
            RemovedFromGroups = true,
            AccountExpirationSet = true
        };

        if (root.TryGetProperty("GrupiuNarystes", out var groups))
        {
            foreach (var g in groups.EnumerateArray())
            {
                record.GroupMemberships.Add(new GroupMembershipRecord
                {
                    GroupName = g.TryGetProperty("GroupName", out var gn) ? gn.GetString() ?? "" : "",
                    GroupDN = g.TryGetProperty("GroupDN", out var gdn) ? gdn.GetString() ?? "" : ""
                });
            }
        }

        return record;
    }

    private static TerminationRecord? ParseCsvExport(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<GroupMembershipRecord>();
        var inGroups = false;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                inGroups = false;
                continue;
            }

            var parts = line.Split(';', 2);
            if (parts.Length < 2) continue;

            if (parts[0] == "Grupė" && parts[1] == "Distinguished Name")
            {
                inGroups = true;
                continue;
            }

            if (inGroups)
            {
                groups.Add(new GroupMembershipRecord
                {
                    GroupName = parts[0],
                    GroupDN = parts[1]
                });
            }
            else
            {
                fields[parts[0]] = parts[1];
            }
        }

        if (!fields.ContainsKey("SamAccountName")) return null;

        return new TerminationRecord
        {
            SamAccountName = fields.GetValueOrDefault("SamAccountName", ""),
            DisplayName = fields.GetValueOrDefault("DisplayName", ""),
            DistinguishedName = fields.GetValueOrDefault("DistinguishedName", ""),
            OriginalOU = fields.GetValueOrDefault("OrganizationalUnit"),
            AccountDisabled = true,
            MovedToDisabledOU = true,
            RemovedFromGroups = groups.Count > 0,
            AccountExpirationSet = true,
            GroupMemberships = groups
        };
    }

    private static TerminationRecord? ParseXmlExport(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root;
        if (root == null) return null;

        var info = root.Element("VartotojoInfo");
        if (info == null) return null;

        var record = new TerminationRecord
        {
            SamAccountName = info.Element("SamAccountName")?.Value ?? "",
            DisplayName = info.Element("DisplayName")?.Value ?? "",
            DistinguishedName = info.Element("DistinguishedName")?.Value ?? "",
            OriginalOU = info.Element("OrganizationalUnit")?.Value,
            AccountDisabled = true,
            MovedToDisabledOU = true,
            RemovedFromGroups = true,
            AccountExpirationSet = true
        };

        var groupsEl = root.Element("GrupiuNarystes");
        if (groupsEl != null)
        {
            foreach (var g in groupsEl.Elements("Grupe"))
            {
                record.GroupMemberships.Add(new GroupMembershipRecord
                {
                    GroupName = g.Element("Pavadinimas")?.Value ?? "",
                    GroupDN = g.Element("DN")?.Value ?? ""
                });
            }
        }

        return record;
    }

    private async void OnFireUser(object sender, RoutedEventArgs e)
    {
        var displayItem = ResolveTargetUser(sender);
        if (displayItem == null)
            return;

        var user = displayItem.Model;

        if (_terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Termination is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        List<string>? organizationalUnits = null;
        try
        {
            organizationalUnits = _terminationService.GetOrganizationalUnits();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load OU list", ex);
        }

        var fireDialog = new FireUserDialog(user, organizationalUnits);
        if (fireDialog.ShowDialog() != true)
            return;

        var confirmDialog = new TerminationConfirmationDialog(user, fireDialog);
        if (confirmDialog.ShowDialog() != true)
            return;

        var progressDialog = new TerminationProgressDialog(
            user, _terminationService, _databaseService!, _backupService!, fireDialog, _mainWindow!.MementoCaretaker);
        progressDialog.ShowDialog();

        if (progressDialog.Result != null &&
            progressDialog.Result.StepResults.Any() &&
            progressDialog.Result.StepResults
                .Where(step => !string.Equals(step.StepName, "Save to database", StringComparison.OrdinalIgnoreCase))
                .All(step => step.Success))
        {
            await TryLogAdminActionAsync("Terminate user", user, $"Termination workflow completed for {user.SamAccountName}");
        }

        LoadUsers();
    }

    private async void OnEditUser(object sender, RoutedEventArgs e)
    {
        var displayItem = ResolveTargetUser(sender);
        if (displayItem == null)
            return;

        if (_adService == null || _groupService == null || _terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Editing is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        List<string> placementTargets;
        List<GroupMembershipRecord> groups;
        try
        {
            placementTargets = _terminationService.GetUserPlacementTargets();
            groups = _terminationService.GetUserGroups(displayItem.Model.DistinguishedName);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Load edit form data: {displayItem.Model.SamAccountName}", ex);
            System.Windows.MessageBox.Show($"Failed to prepare the edit form:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new EditUserDialog(displayItem.Model, _groupService, placementTargets, groups)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result == null)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var updatedUser = _adService.UpdateUser(dialog.Result);
            await TryLogAdminActionAsync("Edit user", updatedUser, $"Updated user details for {updatedUser.SamAccountName}");
            LoadUsersAndSelect(updatedUser.SamAccountName);

            System.Windows.MessageBox.Show(
                $"User updated: {updatedUser.DisplayName} ({updatedUser.SamAccountName})",
                "User Updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Edit user: {dialog.Result.OriginalSamAccountName}", ex);
            System.Windows.MessageBox.Show($"Failed to update user:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void OnDeleteUser(object sender, RoutedEventArgs e)
    {
        var displayItem = ResolveTargetUser(sender);
        if (displayItem == null)
            return;

        if (_adService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Delete is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var user = displayItem.Model;
        var wasSelected = UsersGrid.SelectedItem is UserDisplayItem selected &&
            string.Equals(selected.SamAccountName, user.SamAccountName, StringComparison.OrdinalIgnoreCase);
        var confirmDialog = new DeleteUserConfirmationDialog(user)
        {
            Owner = Window.GetWindow(this)
        };

        if (confirmDialog.ShowDialog() != true)
            return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _adService.DeleteUser(user.SamAccountName);
            var cleanupSummary = await CleanupDeletedUserArtifactsAsync(user);
            await TryLogAdminActionAsync("Delete user", user, cleanupSummary);
            LoadUsers();

            if (wasSelected)
            {
                UsersGrid.SelectedItem = null;
                UserDetailsPanel.Visibility = Visibility.Collapsed;
                NoSelectionMessage.Visibility = Visibility.Visible;
            }

            System.Windows.MessageBox.Show(
                $"User deleted: {user.DisplayName} ({user.SamAccountName})",
                "User Deleted",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Delete user: {user.SamAccountName}", ex);
            System.Windows.MessageBox.Show($"Failed to delete user:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async Task<string> CleanupDeletedUserArtifactsAsync(ADUserModel user)
    {
        var cleanupMessages = new List<string> { $"Deleted user {user.SamAccountName}" };
        var cleanupWarnings = new List<string>();

        if (_mainWindow?.MementoCaretaker != null)
        {
            var removed = _mainWindow.MementoCaretaker.RemoveAll(user.SamAccountName);
            cleanupMessages.Add(removed
                ? "Removed local mementos"
                : "No local mementos found");
        }

        if (_databaseService == null || _backupService == null)
            return string.Join("; ", cleanupMessages);

        try
        {
            await _databaseService.EnsureInitializedAsync();
            if (!_databaseService.IsAvailable)
            {
                cleanupWarnings.Add("database cleanup skipped because the database is unavailable");
            }
            else
            {
                var companyId = await _backupService.GetOrRegisterCompanyAsync();
                if (companyId.HasValue)
                {
                    await _databaseService.ArchiveUserBackupsAsync(user.SamAccountName, companyId.Value);
                    cleanupMessages.Add("Archived active DB backups");
                }
                else
                {
                    cleanupWarnings.Add("database backup archive skipped because company configuration was unavailable");
                }

                await _databaseService.MarkTerminationRecordsAsDeletedAsync(user.SamAccountName);
                cleanupMessages.Add("Marked termination history as deleted");
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Delete user cleanup: {user.SamAccountName}", ex);
            cleanupWarnings.Add($"cleanup incomplete: {ex.Message}");
        }

        if (cleanupWarnings.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "User was deleted from Active Directory, but some restore/audit cleanup steps could not be completed:\n\n" +
                string.Join("\n", cleanupWarnings),
                "Cleanup Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return string.Join("; ", cleanupMessages.Concat(cleanupWarnings));
    }

    private async void OnResetPassword(object sender, RoutedEventArgs e)
    {
        var displayItem = ResolveTargetUser(sender);
        if (displayItem == null)
            return;

        if (_terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. Password reset is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var user = displayItem.Model;
        var dialog = new ResetPasswordDialog(user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            _terminationService.ResetPassword(
                user.SamAccountName,
                dialog.GeneratedPassword,
                dialog.ForceChangeAtNextSignIn);

            try
            {
                var auditRecord = new PasswordResetRecord
                {
                    SamAccountName = user.SamAccountName,
                    DisplayName = user.DisplayName,
                    DistinguishedName = user.DistinguishedName,
                    ForceChangeAtNextSignIn = dialog.ForceChangeAtNextSignIn
                };

                var auditId = await _databaseService!.SavePasswordResetRecordAsync(auditRecord);
                if (auditId <= 0)
                {
                    ErrorLogger.Log($"Password reset audit skipped: {user.SamAccountName}",
                        new InvalidOperationException("Database unavailable"));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log($"Password reset audit: {user.SamAccountName}", ex);
            }

            await TryLogAdminActionAsync(
                "Reset password",
                user,
                dialog.ForceChangeAtNextSignIn
                    ? "Password reset and forced change at next sign-in"
                    : "Password reset without forced change at next sign-in");

            LoadUsersAndSelect(user.SamAccountName);

            var credentialsDialog = new PasswordResetCredentialsDialog(new PasswordResetCredentials
            {
                SamAccountName = user.SamAccountName,
                Password = dialog.GeneratedPassword,
                ForceChangeAtNextSignIn = dialog.ForceChangeAtNextSignIn
            })
            {
                Owner = Window.GetWindow(this)
            };

            credentialsDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Reset password: {user.SamAccountName}", ex);
            System.Windows.MessageBox.Show($"Failed to reset password:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    public Task OpenAddUserAsync() => ShowAddUserDialogAsync();

    private async void OnAddUser(object sender, RoutedEventArgs e)
    {
        await ShowAddUserDialogAsync();
    }

    private async Task ShowAddUserDialogAsync()
    {
        if (_adService == null || _groupService == null || _userProvisioningService == null || _terminationService == null)
        {
            System.Windows.MessageBox.Show("AD service is unavailable. User creation is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        List<string> placementTargets;
        try
        {
            placementTargets = _terminationService.GetUserPlacementTargets();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Load OU list for create user", ex);
            System.Windows.MessageBox.Show($"Failed to load the OU list:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new CreateUserDialog(_userProvisioningService, _groupService, placementTargets)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Result?.User == null)
            return;

        var result = dialog.Result;
        await TryLogAdminActionAsync("Create user", result.User, $"Created new user {result.User.SamAccountName}");
        LoadUsersAndSelect(result.User.SamAccountName);

        var credentialsDialog = new UserCredentialsDialog(result)
        {
            Owner = Window.GetWindow(this)
        };
        credentialsDialog.ShowDialog();
    }

    private UserDisplayItem? ResolveTargetUser(object sender)
    {
        if (sender is FrameworkElement { DataContext: UserDisplayItem displayItem })
        {
            return displayItem;
        }

        return UsersGrid.SelectedItem as UserDisplayItem;
    }

    private void UpdateSelectedUserActionState()
    {
        var hasSelection = UsersGrid?.SelectedItem is UserDisplayItem;

        if (EditUserButton != null)
            EditUserButton.IsEnabled = hasSelection && _adService != null && _groupService != null && _terminationService != null;

        if (DeleteUserButton != null)
            DeleteUserButton.IsEnabled = hasSelection && _adService != null;

        if (ResetPasswordButton != null)
            ResetPasswordButton.IsEnabled = hasSelection && _terminationService != null;

        if (TerminateUserButton != null)
            TerminateUserButton.IsEnabled = hasSelection && _terminationService != null;
    }

    private async Task TryLogAdminActionAsync(string actionType, ADUserModel user, string details)
    {
        if (_databaseService == null)
            return;

        try
        {
            await _databaseService.SaveAdminActionAsync(new AdminActionRecord
            {
                ActionType = actionType,
                SamAccountName = user.SamAccountName,
                DisplayName = user.DisplayName,
                Details = details
            });
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Admin action audit: {actionType}", ex);
        }
    }
}

public class UserDisplayItem
{
    public ADUserModel Model { get; }

    public string DisplayName => Model.DisplayName;
    public string SamAccountName => Model.SamAccountName;
    public string Email => Model.Email;
    public string Department => Model.Department;
    public string StatusText
    {
        get
        {
            var t = TranslationSource.Instance;
            return !Model.IsEnabled ? t["UserStatus_Disabled"] : Model.IsLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
        }
    }
    public string LastLogonText => Model.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? TranslationSource.Instance["Value_Never"];

    public UserDisplayItem(ADUserModel model)
    {
        Model = model;
    }
}
