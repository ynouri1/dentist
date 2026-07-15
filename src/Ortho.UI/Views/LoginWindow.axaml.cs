using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ortho.UI.Localization;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class LoginWindow : Window
{
    /// <summary>Archive de sauvegarde choisie et confirmée : App effectue la restauration.</summary>
    public Func<string, Task>? RestoreRequested { get; set; }

    public LoginWindow()
    {
        InitializeComponent();
    }

    private async void OnForgotPasswordClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel viewModel)
            return;

        var username = await InputDialog.ShowAsync(
            this, L.Get("ForgotPassword"), L.Get("RecoveryUsernamePrompt"));
        if (string.IsNullOrWhiteSpace(username))
            return;

        var code = await InputDialog.ShowAsync(
            this, L.Get("ForgotPassword"), L.Get("RecoveryCodePrompt"));
        if (string.IsNullOrWhiteSpace(code))
            return;

        var newPassword = await InputDialog.ShowAsync(
            this, L.Get("ForgotPassword"), L.Get("NewPasswordPrompt"), masked: true);
        if (string.IsNullOrWhiteSpace(newPassword))
            return;

        var newCode = await viewModel.ResetPasswordAsync(username, code, newPassword);
        if (newCode is not null)
        {
            viewModel.Username = username;
            await InfoDialog.ShowAsync(
                this, L.Get("RecoveryCodeTitle"), L.Get("RecoveryNewCodeMessage"), newCode);
        }
    }

    private async void OnRestoreBackupClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L.Get("RestoreBackup"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(L.Get("BackupFilterName")) { Patterns = ["*.zip"] },
            ],
        });
        if (files.Count == 0)
            return;

        if (!await ConfirmDialog.ShowAsync(this, L.Get("RestoreConfirmMessage"), L.Get("Restore")))
            return;

        if (RestoreRequested is { } handler)
            await handler(files[0].Path.LocalPath);
    }
}
