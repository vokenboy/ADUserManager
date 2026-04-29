using System.Windows;
using ActiveManager.Helpers;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class ResetPasswordDialog : Window
{
    private readonly ADUserModel _user;

    public string GeneratedPassword { get; private set; } = string.Empty;
    public bool ForceChangeAtNextSignIn => ForceChangeCheck.IsChecked == true;

    public ResetPasswordDialog(ADUserModel user)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        _user = user;
        Title = $"Reset Password: {user.DisplayName}";
        HeaderText.Text = $"Reset password for {user.DisplayName} ({user.SamAccountName})";
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        GeneratedPassword = UserProvisioningService.GenerateSecurePassword();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
