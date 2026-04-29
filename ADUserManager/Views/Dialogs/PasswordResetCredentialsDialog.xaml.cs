using System.Windows;
using ActiveManager.Helpers;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class PasswordResetCredentialsDialog : Window
{
    private readonly PasswordResetCredentials _credentials;

    public PasswordResetCredentialsDialog(PasswordResetCredentials credentials)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _credentials = credentials;
        HeaderText.Text = $"Password reset: {credentials.SamAccountName}";
        UsernameBox.Text = credentials.SamAccountName;
        PasswordBox.Text = credentials.Password;
        DetailsText.Text = credentials.ForceChangeAtNextSignIn
            ? "The user must change this temporary password at next sign-in."
            : "The temporary password is active immediately.";
        CopyStatusText.Text = "You can copy the username and password to the clipboard.";
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(BuildClipboardText(_credentials));
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

    internal static string BuildClipboardText(PasswordResetCredentials credentials)
    {
        return
            $"Username: {credentials.SamAccountName}{Environment.NewLine}" +
            $"Password: {credentials.Password}";
    }
}
