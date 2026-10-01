using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as SettingsViewModel)?.Detach();
    }

    private async void CreateBackup_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder for the backup",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return;
        }

        bool includeStats = this.FindControl<CheckBox>("IncludeStatsInBackupCheckBox")?.IsChecked == true;

        try
        {
            BackupManager.TryCreateBackup(folders[0].Path.LocalPath, includeStats);
            SetBackupStatus("Backup created successfully.");
        }
        catch (Exception ex)
        {
            SetBackupStatus($"Backup failed: {ex.Message}");
        }
    }

    private async void RestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a backup file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Backup Files") { Patterns = ["*.zip"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        var confirmDialog = new ConfirmDialogWindow(
            "Restoring replaces all of your current decks, groups, folders and settings with the ones in the backup. Continue?"
        );
        if (!await confirmDialog.ShowDialog<bool>(this))
        {
            return;
        }

        try
        {
            BackupManager.TryRestoreBackup(files[0].Path.LocalPath);

            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshFromMetadata();
            }

            if (Owner is MainWindow { DataContext: DashboardViewModel mainVm })
            {
                mainVm.RefreshAfterBackupRestore();
            }

            SetBackupStatus("Backup restored successfully.");
        }
        catch (Exception ex)
        {
            SetBackupStatus($"Restore failed: {ex.Message}");
        }
    }

    private void SetBackupStatus(string message)
    {
        var statusText = this.FindControl<TextBlock>("BackupStatusText");
        if (statusText == null)
        {
            return;
        }

        statusText.Text = message;
        statusText.IsVisible = true;
    }

    private async void DeleteAllStats_Click(object? sender, RoutedEventArgs e)
    {
        var confirmDialog = new ConfirmDialogWindow(
            "Are you sure you want to delete all flashcard stats across all decks?"
        );

        bool confirmed = await confirmDialog.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        FlashCardRepository.DeleteAllStats();

        if (Owner is MainWindow { DataContext: DashboardViewModel mainVm })
        {
            mainVm.RefreshStats();
        }

        var statusText = this.FindControl<TextBlock>("DeleteStatsStatusText");
        if (statusText != null)
        {
            statusText.Text = "All flashcard stats were deleted.";
            statusText.IsVisible = true;
        }
    }

    private async void OpenDeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        var window = new DeleteDeckStatsWindow();
        window.StatsDeleted += () =>
        {
            if (Owner is MainWindow { DataContext: DashboardViewModel mainVm }) mainVm.RefreshStats();
        };
        await window.ShowDialog(this);
    }

    // --- Account ---

    private async void SignIn_Click(object? sender, RoutedEventArgs e)
    {
        // The account section updates itself from the stored session.
        await new LoginWindow().ShowDialog<bool>(this);
    }

    private async void SignOut_Click(object? sender, RoutedEventArgs e)
    {
        await AuthSession.SignOutAsync();
        SetAccountStatus(null);
    }

    private async void ChangeUsername_Click(object? sender, RoutedEventArgs e)
    {
        const int min = LoginViewModel.MinUsernameLength, max = LoginViewModel.MaxUsernameLength;

        var prompt = new TextPromptWindow("Change username", "Save", AuthSession.Username,
            $"{min}-{max} characters. Shown on the decks you share.");
        var username = await prompt.ShowDialog<string?>(this);
        if (username is null || username == AuthSession.Username) return;

        if (username.Length < min || username.Length > max)
        {
            SetAccountStatus($"Usernames must be {min}-{max} characters.");
            return;
        }

        SetAccountStatus("Saving username...");
        var result = await AuthSession.ChangeUsernameAsync(username);
        SetAccountStatus(result.Success ? $"Username changed to {username}." : $"Couldn't change username: {result.Message}");
    }

    private void SetAccountStatus(string? message)
    {
        var statusText = this.FindControl<TextBlock>("AccountStatusText");
        if (statusText == null) return;

        statusText.Text = message;
        statusText.IsVisible = message is not null;
    }

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
    {
        var statusText = this.FindControl<TextBlock>("UpdateStatusText");
        if (statusText != null)
        {
            statusText.Text = "Checking for updates...";
            statusText.IsVisible = true;
        }

        var updateClient = new UpdateClient();
        var updateInfo = await updateClient.CheckForUpdatesAsync();

        if (updateInfo == null)
        {
            statusText?.Text = "You are already on the latest version.";
            return;
        }

        statusText?.Text = $"Version {updateInfo.TargetFullRelease.Version} available!";
        var confirmDialog = new ConfirmDialogWindow(
        $"Version {updateInfo.TargetFullRelease.Version} is available. Download and restart now?"
    );

        bool confirmed = await confirmDialog.ShowDialog<bool>(this);

        if (confirmed)
        {
            statusText?.Text = "Downloading update... 0%";

            bool applied = await updateClient.DownloadAndApplyUpdateAsync(updateInfo, progress =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    statusText?.Text = $"Downloading update... {progress}%";
                });
            });

            // Success restarts the app, so getting here means it failed.
            if (!applied) statusText?.Text = "The update couldn't be installed. Please try again later.";
        }
        else
        {
            if (statusText != null) statusText.Text = "Update cancelled.";
        }
    }
}