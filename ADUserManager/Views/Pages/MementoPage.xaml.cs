using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services;
using ActiveManager.Services.Models;
using ActiveManager.Views.Dialogs;

namespace ActiveManager.Views.Pages;

public partial class MementoPage : Page, IServiceConsumer
{
    private ITerminationService? _terminationService;
    private DatabaseService? _databaseService;
    private BackupService? _backupService;
    private MainWindow? _mainWindow;
    private MementoCaretaker? _caretaker;

    public ObservableCollection<MementoDisplayItem> Mementos { get; } = new();
    public ObservableCollection<DbBackupDisplayItem> DbBackups { get; } = new();

    public MementoPage()
    {
        InitializeComponent();
        MementoGrid.ItemsSource = Mementos;
        DbGrid.ItemsSource = DbBackups;
    }

    public void SetServices(UserService? adService, ITerminationService? terminationService, DatabaseService databaseService, MainWindow mainWindow)
    {
        _terminationService = terminationService;
        _databaseService = databaseService;
        _backupService = mainWindow.BackupService;
        _mainWindow = mainWindow;
        _caretaker = mainWindow.MementoCaretaker;

        LoadMementos();
        _ = LoadDbBackupsAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TranslationSource.Instance.CultureChanged += OnCultureChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        TranslationSource.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged()
    {
        FilterMementos(SearchBox.Text.Trim().ToLowerInvariant() is { Length: > 0 } term ? term : null);
        _ = LoadDbBackupsAsync(DbSearchBox.Text.Trim() is { Length: > 0 } t ? t : null);
    }

    private void LoadMementos()
    {
        FilterMementos(null);
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        LoadMementos();
    }

    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        var term = SearchBox.Text.Trim().ToLowerInvariant();
        FilterMementos(term);
    }

    private void FilterMementos(string? searchTerm)
    {
        if (_caretaker == null) return;

        var t = TranslationSource.Instance;
        Mementos.Clear();
        var allMementos = _caretaker.GetAll();

        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? allMementos
            : allMementos.Where(m =>
                m.DisplayName.ToLowerInvariant().Contains(searchTerm) ||
                m.SamAccountName.ToLowerInvariant().Contains(searchTerm));

        foreach (var memento in filtered)
            Mementos.Add(new MementoDisplayItem(memento));

        MementoCountText.Text = $"  ({Mementos.Count})";

        if (Mementos.Count > 0)
        {
            NoMementosMessage.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoMementosText.Text = string.IsNullOrWhiteSpace(searchTerm)
                ? t["Restore_NoSavedChanges"]
                : t["Restore_NoMatch"];
            NoMementosTitle.Text = t["Restore_SelectRecord_Title"];
            NoMementosMessage.Visibility = Visibility.Visible;
            MementoDetailsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMementoSelected(object sender, SelectionChangedEventArgs e)
    {
        if (MementoGrid.SelectedItem is MementoDisplayItem selected)
        {
            UpdateDetails(selected.Memento);
            MementoDetailsPanel.Visibility = Visibility.Visible;
            NoMementosMessage.Visibility = Visibility.Collapsed;
        }
        else
        {
            MementoDetailsPanel.Visibility = Visibility.Collapsed;
            if (_caretaker != null && _caretaker.Count > 0)
            {
                var t = TranslationSource.Instance;
                NoMementosTitle.Text = t["Restore_SelectRecord_Title"];
                NoMementosText.Text = t["Restore_SelectRecord_Body"];
                NoMementosMessage.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateDetails(UserMemento memento)
    {
        var t = TranslationSource.Instance;
        var location = FormatDetailValue(memento.OrganizationalUnit);
        var status = GetStatusText(memento.IsEnabled, memento.IsLockedOut);
        var capturedAt = memento.CapturedAt.ToString("yyyy-MM-dd HH:mm");

        DetailsHeader.Text = memento.DisplayName;
        DetailIdentitySubheader.Text = string.Format(t["Restore_SubHeader"], memento.SamAccountName, capturedAt);
        DetailIdentityNote.Text = string.Format(t["Restore_IdentityNote_Local"], location);
        DetailDisplayName.Text = FormatDetailValue(memento.DisplayName);
        DetailSamAccountName.Text = FormatDetailValue(memento.SamAccountName);
        DetailDN.Text = FormatDetailValue(memento.DistinguishedName);
        DetailOU.Text = location;
        DetailStateText.Text = status;
        DetailIsEnabled.Text = memento.IsEnabled ? t["Value_Yes"] : t["Value_No"];
        DetailIsLockedOut.Text = memento.IsLockedOut ? t["Value_Yes"] : t["Value_No"];
        DetailExpiration.Text = memento.AccountExpiration?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        DetailCapturedAt.Text = capturedAt;
        DetailCreatedBy.Text = FormatDetailValue(memento.CapturedBy);
        DetailStatusBadgeText.Text = status;
        DetailGroupCountSummary.Text = memento.GroupMemberships.Count == 1 ? t["Value_OneGroup"] : string.Format(t["Value_NGroups"], memento.GroupMemberships.Count);
        DetailCapturedSummary.Text = capturedAt;
        DetailCreatedBySummary.Text = FormatDetailValue(memento.CapturedBy);
        DetailLocationSummary.Text = location;
        DetailEnabledSummary.Text = memento.IsEnabled ? t["Value_Yes"] : t["Value_No"];
        DetailLockedSummary.Text = memento.IsLockedOut ? t["Value_Yes"] : t["Value_No"];

        DetailGroupsList.ItemsSource = memento.GroupMemberships.Count > 0
            ? memento.GroupMemberships.Select(g => g.GroupName).ToList()
            : new[] { t["Value_NoGroupsFound"] };

        ApplyStatusTheme(DetailStatusBadgeBorder, DetailStatusBadgeText, memento.IsEnabled, memento.IsLockedOut);
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var item = ResolveSelectedMemento(sender);
        if (item == null)
            return;

        if (_terminationService == null)
        {
            MessageBox.Show("AD service is unavailable. Restore is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var memento = item.Memento;

        var confirm = MessageBox.Show(
            $"Restore this user state?\n\n" +
            $"User: {memento.DisplayName} ({memento.SamAccountName})\n" +
            $"Captured: {memento.CapturedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"Location: {memento.OrganizationalUnit}\n" +
            $"Groups: {memento.GroupMemberships.Count}\n\n" +
            $"This restores account state, OU, group membership, and expiration.",
            "Restore State",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var record = new TerminationRecord
        {
            SamAccountName = memento.SamAccountName,
            DisplayName = memento.DisplayName,
            DistinguishedName = memento.DistinguishedName,
            OriginalOU = memento.OrganizationalUnit,
            Memento = memento,
            GroupMemberships = memento.GroupMemberships.ToList(),
            AccountDisabled = memento.IsEnabled,
            MovedToDisabledOU = true,
            RemovedFromGroups = memento.GroupMemberships.Count > 0,
            AccountExpirationSet = true
        };

        var rollbackDialog = new RollbackProgressDialog(
            record, _terminationService, _databaseService!, _mainWindow!.MementoCaretaker);
        rollbackDialog.ShowDialog();

        if (rollbackDialog.WasSuccessful)
        {
            await TryLogAdminActionAsync("Restore user", memento.SamAccountName, memento.DisplayName, "Restored user from local memento");
            if (_databaseService != null && _backupService != null)
            {
                try
                {
                    var companyId = await _backupService.GetOrRegisterCompanyAsync();
                    if (companyId.HasValue)
                    {
                        await _databaseService.ArchiveUserBackupsAsync(memento.SamAccountName, companyId.Value);
                    }
                }
                catch
                {
                }

                await LoadDbBackupsAsync();
            }
        }

        LoadMementos();
    }

    private async Task LoadDbBackupsAsync(string? searchTerm = null)
    {
        if (_backupService == null) return;

        var t = TranslationSource.Instance;
        DbNoRecordsText.Text = t["Restore_Loading"];
        DbNoRecordsMessage.Visibility = Visibility.Visible;
        DbDetailsPanel.Visibility = Visibility.Collapsed;

        try
        {
            var backups = await _backupService.GetCompanyBackupsAsync(searchTerm: searchTerm);

            DbBackups.Clear();
            foreach (var backup in backups)
                DbBackups.Add(new DbBackupDisplayItem(backup));

            DbCountText.Text = $"  ({backups.Count})";

            if (backups.Count > 0)
            {
                DbNoRecordsMessage.Visibility = Visibility.Collapsed;
            }
            else
            {
                DbNoRecordsText.Text = string.IsNullOrWhiteSpace(searchTerm)
                    ? t["Restore_DbNoRecords"]
                    : string.Format(t["Restore_DbNoMatch"], searchTerm);
                DbNoRecordsTitle.Text = t["Restore_SelectRecord_Title"];
                DbNoRecordsMessage.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            DbBackups.Clear();
            DbCountText.Text = "  (0)";
            DbNoRecordsText.Text = $"Error loading data: {ex.Message}";
            DbNoRecordsMessage.Visibility = Visibility.Visible;
        }
    }

    private void OnDbRefresh(object sender, RoutedEventArgs e)
    {
        DbSearchBox.Text = string.Empty;
        _ = LoadDbBackupsAsync();
    }

    private void OnDbSearch(object sender, TextChangedEventArgs e)
    {
        var term = DbSearchBox.Text.Trim();
        _ = LoadDbBackupsAsync(string.IsNullOrWhiteSpace(term) ? null : term);
    }

    private void OnDbSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DbGrid.SelectedItem is DbBackupDisplayItem selected)
        {
            UpdateDbDetails(selected);
            DbDetailsPanel.Visibility = Visibility.Visible;
            DbNoRecordsMessage.Visibility = Visibility.Collapsed;
        }
        else
        {
            DbDetailsPanel.Visibility = Visibility.Collapsed;
            if (DbBackups.Count > 0)
            {
                var t = TranslationSource.Instance;
                DbNoRecordsTitle.Text = t["Restore_SelectRecord_Title"];
                DbNoRecordsText.Text = t["Restore_SelectRecord_Body"];
                DbNoRecordsMessage.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateDbDetails(DbBackupDisplayItem item)
    {
        var t = TranslationSource.Instance;
        var b = item.Backup;
        var location = FormatDetailValue(b.OrganizationalUnit);
        var status = GetStatusText(b.IsEnabled, b.IsLockedOut);
        var createdAt = b.CreatedAt.ToString("yyyy-MM-dd HH:mm");

        DbDetailsHeader.Text = b.DisplayName;
        DbDetailIdentitySubheader.Text = string.Format(t["Restore_SubHeader"], b.SamAccountName, createdAt);
        DbDetailIdentityNote.Text = string.Format(t["Restore_IdentityNote_Remote"], location);
        DbDetailDisplayName.Text = FormatDetailValue(b.DisplayName);
        DbDetailSamAccountName.Text = FormatDetailValue(b.SamAccountName);
        DbDetailDN.Text = FormatDetailValue(b.DistinguishedName);
        DbDetailOU.Text = location;
        DbDetailStateText.Text = status;
        DbDetailIsEnabled.Text = b.IsEnabled ? t["Value_Yes"] : t["Value_No"];
        DbDetailIsLockedOut.Text = b.IsLockedOut ? t["Value_Yes"] : t["Value_No"];
        DbDetailExpiration.Text = b.AccountExpiration?.ToString("yyyy-MM-dd HH:mm") ?? t["Value_Never"];
        DbDetailCreatedAt.Text = createdAt;
        DbDetailCreatedBy.Text = FormatDetailValue(b.CreatedBy);
        DbDetailStatusBadgeText.Text = status;
        DbDetailGroupCountSummary.Text = b.GroupCountSnapshot == 1 ? t["Value_OneGroup"] : string.Format(t["Value_NGroups"], b.GroupCountSnapshot);
        DbDetailCapturedSummary.Text = createdAt;
        DbDetailCreatedBySummary.Text = FormatDetailValue(b.CreatedBy);
        DbDetailLocationSummary.Text = location;
        DbDetailEnabledSummary.Text = b.IsEnabled ? t["Value_Yes"] : t["Value_No"];
        DbDetailLockedSummary.Text = b.IsLockedOut ? t["Value_Yes"] : t["Value_No"];

        DbDetailGroupsList.ItemsSource = b.Groups.Count > 0
            ? b.Groups.Select(g => g.GroupName).ToList()
            : new[] { t["Value_GroupsFromSnapshot"] };

        ApplyStatusTheme(DbDetailStatusBadgeBorder, DbDetailStatusBadgeText, b.IsEnabled, b.IsLockedOut);
    }

    private async void OnDbRestore(object sender, RoutedEventArgs e)
    {
        var item = ResolveSelectedDbBackup(sender);
        if (item == null)
            return;

        if (_terminationService == null)
        {
            MessageBox.Show("AD service is unavailable. Restore is not available.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_backupService == null)
        {
            MessageBox.Show("Database backup service is unavailable.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var b = item.Backup;

        var confirm = MessageBox.Show(
            $"Restore this user state?\n\n" +
            $"User: {b.DisplayName} ({b.SamAccountName})\n" +
            $"Captured: {b.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
            $"Location: {b.OrganizationalUnit}\n" +
            $"Groups: {b.GroupCountSnapshot}\n\n" +
            $"This restores account state, OU, group membership, and expiration.\n" +
            $"After a successful restore, the backup will be archived.",
            "Restore State (DB)",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        ADUserBackup? fullBackup;
        try
        {
            fullBackup = await _backupService.GetBackupByIdAsync(b.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load backup:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (fullBackup == null)
        {
            MessageBox.Show("Backup was not found in the database.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var groups = fullBackup.Groups
            .Select(g => new GroupMembershipRecord { GroupName = g.GroupName, GroupDN = g.GroupDN })
            .ToList();

        var memento = new UserMemento(
            samAccountName: fullBackup.SamAccountName,
            displayName: fullBackup.DisplayName,
            distinguishedName: fullBackup.DistinguishedName,
            organizationalUnit: fullBackup.OrganizationalUnit,
            isEnabled: fullBackup.IsEnabled,
            isLockedOut: fullBackup.IsLockedOut,
            accountExpiration: fullBackup.AccountExpiration,
            groupMemberships: groups,
            capturedAt: fullBackup.CreatedAt,
            capturedBy: fullBackup.CreatedBy);

        var linkedTerminationId = fullBackup.TerminationRecordId;

        var record = new TerminationRecord
        {
            SamAccountName = fullBackup.SamAccountName,
            DisplayName = fullBackup.DisplayName,
            DistinguishedName = fullBackup.DistinguishedName,
            OriginalOU = fullBackup.OrganizationalUnit,
            Memento = memento,
            GroupMemberships = groups,
            AccountDisabled = fullBackup.IsEnabled,
            MovedToDisabledOU = true,
            RemovedFromGroups = groups.Count > 0,
            AccountExpirationSet = true
        };

        var rollbackDialog = new RollbackProgressDialog(
            record, _terminationService, _databaseService!, _mainWindow!.MementoCaretaker);
        rollbackDialog.ShowDialog();

        if (rollbackDialog.WasSuccessful)
        {
            await TryLogAdminActionAsync("Restore user", fullBackup.SamAccountName, fullBackup.DisplayName, "Restored user from remote backup");
            if (_databaseService != null)
            {
                try
                {
                    await _databaseService.ArchiveUserBackupsAsync(fullBackup.SamAccountName, fullBackup.CompanyId);
                }
                catch
                {
                }
            }

            if (linkedTerminationId.HasValue && _databaseService != null)
            {
                try
                {
                    await _databaseService.MarkAsRolledBackAsync(linkedTerminationId.Value);
                }
                catch
                {
                }
            }

            LoadMementos();
        }

        _ = LoadDbBackupsAsync(DbSearchBox.Text.Trim() is { Length: > 0 } t ? t : null);
    }

    private static string FormatDetailValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string GetStatusText(bool isEnabled, bool isLockedOut)
    {
        var t = TranslationSource.Instance;
        return !isEnabled ? t["UserStatus_Disabled"] : isLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
    }

    private MementoDisplayItem? ResolveSelectedMemento(object? sender)
    {
        if (sender is FrameworkElement { DataContext: MementoDisplayItem item })
            return item;

        return MementoGrid.SelectedItem as MementoDisplayItem;
    }

    private DbBackupDisplayItem? ResolveSelectedDbBackup(object? sender)
    {
        if (sender is FrameworkElement { DataContext: DbBackupDisplayItem item })
            return item;

        return DbGrid.SelectedItem as DbBackupDisplayItem;
    }

    private void ApplyStatusTheme(Border badgeBorder, TextBlock badgeText, bool isEnabled, bool isLockedOut)
    {
        var fillColor = Color.FromRgb(229, 244, 234);
        var borderColor = Color.FromRgb(149, 201, 164);
        var textColor = Color.FromRgb(27, 108, 52);

        if (!isEnabled)
        {
            fillColor = Color.FromRgb(250, 232, 232);
            borderColor = Color.FromRgb(229, 170, 170);
            textColor = Color.FromRgb(155, 40, 40);
        }
        else if (isLockedOut)
        {
            fillColor = Color.FromRgb(255, 242, 223);
            borderColor = Color.FromRgb(233, 193, 130);
            textColor = Color.FromRgb(150, 97, 12);
        }

        badgeBorder.Background = new SolidColorBrush(fillColor);
        badgeBorder.BorderBrush = new SolidColorBrush(borderColor);
        badgeText.Foreground = new SolidColorBrush(textColor);
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

public class MementoDisplayItem
{
    public UserMemento Memento { get; }

    public string DisplayName => Memento.DisplayName;
    public string SamAccountName => Memento.SamAccountName;
    public string OrganizationalUnit => Memento.OrganizationalUnit ?? string.Empty;
    public int GroupCount => Memento.GroupMemberships.Count;
    public string StatusText
    {
        get
        {
            var t = TranslationSource.Instance;
            return !Memento.IsEnabled ? t["UserStatus_Disabled"] : Memento.IsLockedOut ? t["UserStatus_Locked"] : t["UserStatus_Active"];
        }
    }
    public string CapturedAtText => Memento.CapturedAt.ToString("yyyy-MM-dd HH:mm");
    public string CapturedBy => Memento.CapturedBy;

    public MementoDisplayItem(UserMemento memento)
    {
        Memento = memento;
    }
}

public class DbBackupDisplayItem
{
    public ADUserBackup Backup { get; }

    public string DisplayName => Backup.DisplayName;
    public string SamAccountName => Backup.SamAccountName;
    public string OrganizationalUnit => Backup.OrganizationalUnit;
    public int GroupCount => Backup.GroupCountSnapshot;
    public string StatusText
    {
        get
        {
            var t = TranslationSource.Instance;
            return Backup.IsEnabled ? t["UserStatus_Active"] : t["UserStatus_Disabled"];
        }
    }
    public string CreatedAtText => Backup.CreatedAt.ToString("yyyy-MM-dd HH:mm");
    public string CreatedBy => Backup.CreatedBy;

    public DbBackupDisplayItem(ADUserBackup backup)
    {
        Backup = backup;
    }
}
