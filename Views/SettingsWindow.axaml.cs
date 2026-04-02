using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReviFlash.Data;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
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

        try
        {
            BackupManager.TryCreateBackup(folders[0].Path.LocalPath);
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

        try
        {
            BackupManager.TryRestoreFromBackup(files[0].Path.LocalPath);

            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshFromMetadata();
            }

            if (Owner is MainWindow { DataContext: MainWindowViewModel mainVm })
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

        if (Owner is MainWindow { DataContext: MainWindowViewModel mainVm })
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

    private async void DeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { SelectedDeckForStatDeletion: not null } vm)
        {
            var deckName = vm.SelectedDeckForStatDeletion.Name;
            var confirmDialog = new ConfirmDialogWindow(
                $"Are you sure you want to delete all stats for \"{deckName}\"? This cannot be undone."
            );

            bool confirmed = await confirmDialog.ShowDialog<bool>(this);
            if (!confirmed)
            {
                return;
            }

            vm.DeleteStatsForSelectedDeck();

            if (Owner is MainWindow { DataContext: MainWindowViewModel mainVm })
            {
                mainVm.RefreshStats();
            }

            var statusText = this.FindControl<TextBlock>("DeleteStatsStatusText");
            if (statusText != null)
            {
                statusText.Text = $"Stats for \"{deckName}\" were deleted.";
                statusText.IsVisible = true;
            }
        }
    }
}