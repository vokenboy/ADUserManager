using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ActiveManager.Helpers;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class EditUserDialog : Window
{
    private const int GroupSearchResultLimit = 50;

    private readonly ADUserModel _user;
    private readonly GroupService _groupService;
    private readonly ObservableCollection<GroupSelectionItem> _availableGroups = new();
    private readonly ObservableCollection<GroupSelectionItem> _selectedGroups = new();
    private readonly DispatcherTimer _groupSearchDebounceTimer;
    private int _groupSearchVersion;

    public UpdateUserRequest? Result { get; private set; }

    public EditUserDialog(
        ADUserModel user,
        GroupService groupService,
        IEnumerable<string> placementTargets,
        IEnumerable<GroupMembershipRecord> currentGroups)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _user = user;
        _groupService = groupService;
        _groupSearchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _groupSearchDebounceTimer.Tick += OnGroupSearchDebounceElapsed;

        AvailableGroupsList.ItemsSource = _availableGroups;
        SelectedGroupsList.ItemsSource = _selectedGroups;

        foreach (var target in placementTargets.OrderBy(target => target))
        {
            OUCombo.Items.Add(target);
        }

        foreach (var group in currentGroups
                     .Where(group => !string.IsNullOrWhiteSpace(group.GroupDN))
                     .OrderBy(group => group.GroupName))
        {
            _selectedGroups.Add(new GroupSelectionItem(group.GroupName, group.GroupDN));
        }

        FirstNameBox.Text = user.FirstName;
        LastNameBox.Text = user.LastName;
        SamAccountNameBox.Text = user.SamAccountName;
        EmailBox.Text = user.Email;
        DepartmentBox.Text = user.Department;
        TitleBox.Text = user.Title;
        DescriptionBox.Text = user.Description;
        EnabledCheckBox.IsChecked = user.IsEnabled;

        if (!string.IsNullOrWhiteSpace(user.OrganizationalUnit))
        {
            OUCombo.SelectedItem = placementTargets.FirstOrDefault(target =>
                string.Equals(target, user.OrganizationalUnit, StringComparison.OrdinalIgnoreCase));
        }

        if (OUCombo.SelectedItem == null && OUCombo.Items.Count > 0)
        {
            OUCombo.SelectedIndex = 0;
        }

        FirstNameBox.TextChanged += (_, _) => UpdateDisplayNamePreview();
        LastNameBox.TextChanged += (_, _) => UpdateDisplayNamePreview();
        UpdateDisplayNamePreview();
        ResetAvailableGroups();
        UpdateSelectionButtons();
    }

    private async Task LoadAvailableGroupsAsync(string filter)
    {
        var searchVersion = ++_groupSearchVersion;

        try
        {
            SetGroupSearchStatus("Searching groups...");
            AvailableGroupsList.IsEnabled = false;
            AddGroupButton.IsEnabled = false;

            var groups = await _groupService.SearchGroupsAsync(filter);
            if (searchVersion != _groupSearchVersion)
            {
                return;
            }

            var selectedDns = _selectedGroups
                .Select(g => g.DistinguishedName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var matchingGroups = groups
                .Where(g => !selectedDns.Contains(g.DistinguishedName))
                .OrderBy(g => g.Name)
                .Take(GroupSearchResultLimit)
                .Select(g => new GroupSelectionItem(g.Name, g.DistinguishedName))
                .ToList();

            _availableGroups.Clear();
            foreach (var group in matchingGroups)
            {
                _availableGroups.Add(group);
            }

            if (_availableGroups.Count == 0)
            {
                SetGroupSearchStatus("No groups matched this search.");
            }
            else if (groups.Count > GroupSearchResultLimit)
            {
                SetGroupSearchStatus($"Showing the first {GroupSearchResultLimit} results. Narrow the search if needed.");
            }
            else
            {
                SetGroupSearchStatus($"Groups found: {_availableGroups.Count}.");
            }
        }
        catch (Exception ex)
        {
            if (searchVersion != _groupSearchVersion)
            {
                return;
            }

            _availableGroups.Clear();
            ErrorLogger.Log("Load group list while editing user", ex);
            SetValidationMessage($"Failed to load groups: {ex.Message}");
            SetGroupSearchStatus("Failed to load groups.");
        }
        finally
        {
            if (searchVersion == _groupSearchVersion)
            {
                AvailableGroupsList.IsEnabled = true;
                UpdateSelectionButtons();
            }
        }
    }

    private void UpdateDisplayNamePreview()
    {
        var displayName = UserProvisioningService.BuildDisplayName(FirstNameBox.Text, LastNameBox.Text);
        DisplayNamePreview.Text = string.IsNullOrEmpty(displayName)
            ? "Display Name: -"
            : $"Display Name: {displayName}";
    }

    private void OnGroupSearchChanged(object sender, TextChangedEventArgs e)
    {
        ClearValidationMessage();

        var filter = GroupSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            _groupSearchDebounceTimer.Stop();
            _groupSearchVersion++;
            ResetAvailableGroups();
            return;
        }

        SetGroupSearchStatus("Searching after a short pause...");
        _groupSearchDebounceTimer.Stop();
        _groupSearchDebounceTimer.Start();
    }

    private async void OnGroupSearchDebounceElapsed(object? sender, EventArgs e)
    {
        _groupSearchDebounceTimer.Stop();

        var filter = GroupSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            _groupSearchVersion++;
            ResetAvailableGroups();
            return;
        }

        await LoadAvailableGroupsAsync(filter);
    }

    private void OnAddGroup(object sender, RoutedEventArgs e)
    {
        if (AvailableGroupsList.SelectedItem is not GroupSelectionItem selected)
        {
            return;
        }

        _selectedGroups.Add(selected);
        _availableGroups.Remove(selected);
        AvailableGroupsList.SelectedItem = null;
        ClearValidationMessage();
        UpdateSelectionButtons();
        RefreshSearchStatusAfterSelectionChange();
    }

    private void OnAvailableGroupDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AvailableGroupsList.SelectedItem is GroupSelectionItem)
        {
            OnAddGroup(sender, new RoutedEventArgs());
        }
    }

    private void OnRemoveGroup(object sender, RoutedEventArgs e)
    {
        if (SelectedGroupsList.SelectedItem is not GroupSelectionItem selected)
        {
            return;
        }

        _selectedGroups.Remove(selected);
        SelectedGroupsList.SelectedItem = null;
        UpdateSelectionButtons();

        var filter = GroupSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(filter) ||
            !selected.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            RefreshSearchStatusAfterSelectionChange();
            return;
        }

        _availableGroups.Add(selected);
        SortAvailableGroups();
        RefreshSearchStatusAfterSelectionChange();
    }

    private void OnAvailableGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionButtons();
    }

    private void OnSelectedGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionButtons();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ClearValidationMessage();

        var request = new UpdateUserRequest
        {
            OriginalSamAccountName = _user.SamAccountName,
            FirstName = FirstNameBox.Text.Trim(),
            LastName = LastNameBox.Text.Trim(),
            Email = EmailBox.Text.Trim(),
            TargetOU = OUCombo.SelectedItem?.ToString() ?? string.Empty,
            Department = DepartmentBox.Text.Trim(),
            Title = TitleBox.Text.Trim(),
            Description = DescriptionBox.Text.Trim(),
            SelectedGroups = _selectedGroups.Select(g => g.DistinguishedName).ToList(),
            Enabled = EnabledCheckBox.IsChecked == true
        };

        var validationError = UserService.ValidateUpdateRequest(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            SetValidationMessage(validationError);
            return;
        }

        Result = request;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ResetAvailableGroups()
    {
        _availableGroups.Clear();
        AvailableGroupsList.IsEnabled = true;
        SetGroupSearchStatus("Start typing to find groups.");
        UpdateSelectionButtons();
    }

    private void RefreshSearchStatusAfterSelectionChange()
    {
        if (_availableGroups.Count == 0)
        {
            SetGroupSearchStatus("No more groups matched this search.");
            return;
        }

        SetGroupSearchStatus($"Groups found: {_availableGroups.Count}.");
    }

    private void SortAvailableGroups()
    {
        var sortedGroups = _availableGroups
            .OrderBy(group => group.Name)
            .ToList();

        _availableGroups.Clear();
        foreach (var group in sortedGroups)
        {
            _availableGroups.Add(group);
        }
    }

    private void UpdateSelectionButtons()
    {
        AddGroupButton.IsEnabled = AvailableGroupsList.SelectedItem is GroupSelectionItem;
        RemoveGroupButton.IsEnabled = SelectedGroupsList.SelectedItem is GroupSelectionItem;
    }

    private void SetGroupSearchStatus(string message)
    {
        GroupSearchStatusText.Text = message;
    }

    private void SetValidationMessage(string message)
    {
        ValidationMessageText.Text = message;
        ValidationMessageText.Visibility = Visibility.Visible;
    }

    private void ClearValidationMessage()
    {
        ValidationMessageText.Text = string.Empty;
        ValidationMessageText.Visibility = Visibility.Collapsed;
    }

    private sealed class GroupSelectionItem
    {
        public string Name { get; }
        public string DistinguishedName { get; }

        public GroupSelectionItem(string name, string distinguishedName)
        {
            Name = name;
            DistinguishedName = distinguishedName;
        }
    }
}
