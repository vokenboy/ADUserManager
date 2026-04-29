using System.Windows;
using System.Windows.Controls;
using ActiveManager.Helpers;
using ActiveManager.Localization;
using ActiveManager.Services.Models;
using Microsoft.Win32;

namespace ActiveManager.Views.Dialogs;

public partial class FireUserDialog : Window
{
    private readonly ADUserModel _user;
    private string? _generatedPassword;

    public bool DisableAccount => DisableAccountCheck.IsChecked == true;
    public bool MoveToDisabledOU => MoveToOUCheck.IsChecked == true;
    public string? TargetOU => MoveToOUCheck.IsChecked == true ? OUCombo.SelectedItem?.ToString() : null;
    public bool ChangePassword => ChangePasswordCheck.IsChecked == true;
    public string? NewPassword => ChangePasswordCheck.IsChecked == true ? _generatedPassword : null;
    public bool RemoveFromGroups => RemoveFromGroupsCheck.IsChecked == true;
    public bool SetExpiration => SetExpirationCheck.IsChecked == true;
    public DateTime? ExpirationDate => SetExpirationCheck.IsChecked == true ? ExpirationDatePicker.SelectedDate : null;
    public bool ExportData => ExportDataCheck.IsChecked == true;
    public string ExportFormat => (ExportFormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CSV";
    public string? ExportPath => ExportDataCheck.IsChecked == true ? ExportPathText.Text : null;
    public bool ExportGroups => ExportGroupsCheck.IsChecked == true;
    public bool ExportPermissions => ExportPermissionsCheck.IsChecked == true;
    public string TerminationReason => (ReasonCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other";

    public FireUserDialog(ADUserModel user, List<string>? organizationalUnits = null)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _user = user;
        var t = TranslationSource.Instance;
        HeaderText.Text = $"User: {user.DisplayName} ({user.SamAccountName})";

        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Resignation"], IsSelected = true });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Dismissal"] });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_ContractEnded"] });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Retirement"] });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Redundancy"] });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Transfer"] });
        ReasonCombo.Items.Add(new ComboBoxItem { Content = t["Terminate_Reason_Other"] });
        ReasonCombo.SelectedIndex = 0;
        Title = $"Terminate User: {user.DisplayName}";

        if (organizationalUnits != null && organizationalUnits.Count > 0)
        {
            foreach (var ou in organizationalUnits)
                OUCombo.Items.Add(ou);
        }
        else
        {
            OUCombo.Items.Add("OU=Disabled,DC=company,DC=lt");
            OUCombo.Items.Add("OU=Terminated,DC=company,DC=lt");
            OUCombo.Items.Add("OU=Inactive,DC=company,DC=lt");
        }
        OUCombo.SelectedIndex = 0;

        ExportPathText.Text = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{_user.SamAccountName}_export.csv";
        _generatedPassword = GenerateRandomPassword();
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        if (OUCombo == null || ExpirationDatePicker == null || ExportOptionsPanel == null)
            return;

        OUCombo.IsEnabled = MoveToOUCheck.IsChecked == true;
        ExpirationDatePicker.IsEnabled = SetExpirationCheck.IsChecked == true;
        ExportOptionsPanel.IsEnabled = ExportDataCheck.IsChecked == true;
        ExportOptionsPanel.Opacity = ExportDataCheck.IsChecked == true ? 1.0 : 0.5;
    }

    private void OnMoveToOUChanged(object sender, RoutedEventArgs e) => UpdateUIState();
    private void OnExpirationChanged(object sender, RoutedEventArgs e) => UpdateUIState();
    private void OnExportChanged(object sender, RoutedEventArgs e) => UpdateUIState();

    private void OnExportFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportPathText == null || _user == null) return;
        var extension = ExportFormat.ToLower();
        var directory = System.IO.Path.GetDirectoryName(ExportPathText.Text);
        var filename = $"{_user.SamAccountName}_export.{extension}";
        ExportPathText.Text = System.IO.Path.Combine(directory ?? "", filename);
    }

    private void OnBrowseExportPath(object sender, RoutedEventArgs e)
    {
        var extension = ExportFormat.ToLower();
        var dialog = new SaveFileDialog
        {
            Filter = $"{ExportFormat} files (*.{extension})|*.{extension}|All files (*.*)|*.*",
            DefaultExt = extension,
            FileName = $"{_user.SamAccountName}_export.{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dialog.ShowDialog() == true)
        {
            ExportPathText.Text = dialog.FileName;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (ChangePasswordCheck.IsChecked == true && _generatedPassword == null)
        {
            _generatedPassword = GenerateRandomPassword();
        }
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string GenerateRandomPassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 16)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
