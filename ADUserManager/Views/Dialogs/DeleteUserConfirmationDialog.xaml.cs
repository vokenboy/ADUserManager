using System.Windows;
using ActiveManager.Helpers;
using ActiveManager.Services.Models;

namespace ActiveManager.Views.Dialogs;

public partial class DeleteUserConfirmationDialog : Window
{
    public DeleteUserConfirmationDialog(ADUserModel user)
    {
        InitializeComponent();
        WindowBorderHelper.ApplyDialogBorder(this);

        DisplayNameText.Text = string.IsNullOrWhiteSpace(user.DisplayName) ? "-" : user.DisplayName;
        SamAccountNameText.Text = string.IsNullOrWhiteSpace(user.SamAccountName) ? "-" : user.SamAccountName;
        EmailText.Text = string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email;
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
