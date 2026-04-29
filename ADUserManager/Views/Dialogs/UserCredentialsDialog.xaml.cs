using System.Windows;
using ActiveManager.Helpers;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class UserCredentialsDialog : Window
{
    private readonly CreateUserResult _result;

    public UserCredentialsDialog(CreateUserResult result)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _result = result;
        HeaderText.Text = $"User created: {result.User?.DisplayName} ({result.User?.SamAccountName})";
        UsernameBox.Text = result.User?.SamAccountName ?? string.Empty;
        PasswordBox.Text = result.GeneratedPassword;
        CopyStatusText.Text = "You can copy the username and password to the clipboard.";

        var warningMessage = BuildWarningMessage(result);
        if (!string.IsNullOrWhiteSpace(warningMessage))
        {
            WarningsText.Text = warningMessage;
            WarningsPanel.Visibility = Visibility.Visible;
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(BuildClipboardText(_result));
            CopyStatusText.Text = "Credentials copied to the clipboard.";
        }
        catch (Exception ex)
        {
            CopyStatusText.Text = $"Failed to copy the credentials: {ex.Message}";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    internal static string BuildClipboardText(CreateUserResult result)
    {
        return
            $"Username: {result.User?.SamAccountName}{Environment.NewLine}" +
            $"Password: {result.GeneratedPassword}";
    }

    private static string BuildWarningMessage(CreateUserResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return result.ErrorMessage;
        }

        if (result.Warnings.Count > 0)
        {
            return $"Some groups could not be assigned: {string.Join(", ", result.Warnings)}";
        }

        return string.Empty;
    }
}
