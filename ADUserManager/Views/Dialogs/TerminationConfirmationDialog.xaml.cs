using System.Text;
using System.Windows;
using ActiveManager.Helpers;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class TerminationConfirmationDialog : Window
{
    public TerminationConfirmationDialog(ADUserModel user, FireUserDialog options)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        UserNameText.Text = user.DisplayName;
        UserDetailsText.Text = $"Username: {user.SamAccountName}\n" +
                               $"Email: {user.Email}\n" +
                               $"Department: {user.Department}";

        var summary = new StringBuilder();
        var stepNum = 1;

        summary.AppendLine($"  {stepNum++}. Read group memberships (always)");

        if (options.ExportData)
            summary.AppendLine($"  {stepNum++}. Export data ({options.ExportFormat} -> {options.ExportPath})");

        if (options.DisableAccount)
            summary.AppendLine($"  {stepNum++}. Disable account");

        if (options.MoveToDisabledOU)
            summary.AppendLine($"  {stepNum++}. Move to OU: {options.TargetOU}");

        if (options.ChangePassword)
            summary.AppendLine($"  {stepNum++}. Change password (random)");

        if (options.RemoveFromGroups)
            summary.AppendLine($"  {stepNum++}. Remove from all groups");

        if (options.SetExpiration && options.ExpirationDate.HasValue)
            summary.AppendLine($"  {stepNum++}. Set account expiration: {options.ExpirationDate.Value:yyyy-MM-dd}");

        summary.AppendLine($"  {stepNum}. Save to database (always)");

        StepsSummaryText.Text = summary.ToString();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
