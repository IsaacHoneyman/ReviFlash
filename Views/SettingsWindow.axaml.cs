using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReviFlash.Views.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.ViewModels;
using static ReviFlash.Utilities.TextUtility;

namespace ReviFlash.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);
        Closed += (_, _) => (DataContext as SettingsViewModel)?.Detach();
    }

    private DashboardViewModel? Dashboard => Owner?.DataContext as DashboardViewModel;

    private static void ShowStatus(TextBlock statusText, string? message)
    {
        statusText.Text = message;
        statusText.IsVisible = message is not null;
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

        bool includeStats = IncludeStatsInBackupCheckBox.IsChecked == true;

        try
        {
            BackupManager.TryCreateBackup(folders[0].Path.LocalPath, includeStats);
            ShowStatus(BackupStatusText, "Backup created successfully.");
        }
        catch (Exception ex)
        {
            ShowStatus(BackupStatusText, $"Backup failed: {ex.Message}");
        }
    }

    private async void ImportAnki_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an Anki deck",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Anki Decks") { Patterns = ["*.apkg", "*.colpkg"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        AnkiImportButton.IsEnabled = false;
        ShowStatus(AnkiImportStatusText, "Importing...");

        try
        {
            var path = files[0].Path.LocalPath;
            var result = await Task.Run(() => AnkiImporter.Import(path));

            Dashboard?.ReloadLibrary();
            ShowStatus(AnkiImportStatusText, DescribeAnkiImport(result));
        }
        catch (Exception ex)
        {
            Logger.LogError("Anki import failed", ex);
            ShowStatus(AnkiImportStatusText, $"Import failed: {ex.Message}");
        }
        finally
        {
            AnkiImportButton.IsEnabled = true;
        }
    }

    private static string DescribeAnkiImport(AnkiImportResult result)
    {
        var message = $"Imported {Plural(result.Cards, "card")} into {Plural(result.Decks, "deck")}.";
        if (result.CardsWithImages > 0)
            message += $" {Plural(result.CardsWithImages, "card")} had images, which aren't supported yet; they're marked [image] in {string.Join(", ", result.DecksWithImages)}.";
        if (result.Skipped > 0)
            message += $" {Plural(result.Skipped, "note")} with only images (or image occlusion) {(result.Skipped == 1 ? "was" : "were")} left out.";
        return message;
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

            Dashboard?.RefreshAfterBackupRestore();
            ShowStatus(BackupStatusText, "Backup restored successfully.");
        }
        catch (Exception ex)
        {
            ShowStatus(BackupStatusText, $"Restore failed: {ex.Message}");
        }
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
        Dashboard?.ReloadAfterEdit();
        ShowStatus(DeleteStatsStatusText, "All flashcard stats were deleted.");
    }

    private async void OpenDeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        var window = new DeleteDeckStatsWindow();
        window.StatsDeleted += () => Dashboard?.ReloadAfterEdit();
        await window.ShowDialog(this);
    }

    // --- Account ---

    private async void SignIn_Click(object? sender, RoutedEventArgs e)
    {
        // The account section updates itself from the stored session.
        await new LoginWindow().ShowDialog<bool>(this);
    }

    /// <summary> Disables <paramref name="button"/> and shows <paramref name="spinner"/> while <paramref name="work"/> runs. </summary>
    private static async Task<T> RunBusyAsync<T>(object? button, BusySpinner spinner, Func<Task<T>> work)
    {
        if (button is Button b) b.IsEnabled = false;
        spinner.IsVisible = true;

        try { return await work(); }
        finally
        {
            spinner.IsVisible = false;
            if (button is Button done) done.IsEnabled = true;
        }
    }

    private static Task RunBusyAsync(object? button, BusySpinner spinner, Func<Task> work) =>
        RunBusyAsync(button, spinner, async () => { await work(); return true; });

    private async void SignOut_Click(object? sender, RoutedEventArgs e)
    {
        ShowStatus(AccountStatusText, "Signing out...");
        await RunBusyAsync(sender, AccountSpinner, AuthSession.SignOutAsync);
        ShowStatus(AccountStatusText, null);
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
            ShowStatus(AccountStatusText, $"Usernames must be {min}-{max} characters.");
            return;
        }

        ShowStatus(AccountStatusText, "Saving username...");
        var result = await RunBusyAsync(sender, AccountSpinner, () => AuthSession.ChangeUsernameAsync(username));
        ShowStatus(AccountStatusText, result.Success ? $"Username changed to {username}." : $"Couldn't change username: {result.Message}");
    }

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
    {
        ShowStatus(UpdateStatusText, "Checking for updates...");

        var updateClient = new UpdateClient();
        var check = await RunBusyAsync(sender, UpdateSpinner, updateClient.CheckForUpdatesAsync);

        if (check.Status == UpdateCheckStatus.Failed)
        {
            ShowStatus(UpdateStatusText, "Couldn't check for updates. Check your connection.");
            return;
        }

        if (check.Info is not { } updateInfo)
        {
            ShowStatus(UpdateStatusText, "You are already on the latest version.");
            return;
        }

        var version = updateInfo.TargetFullRelease.Version;
        ShowStatus(UpdateStatusText, $"Version {version} available!");
        var confirmDialog = new ConfirmDialogWindow($"Version {version} is available. Download and restart now?");

        if (!await confirmDialog.ShowDialog<bool>(this))
        {
            ShowStatus(UpdateStatusText, "Update cancelled.");
            return;
        }

        // A successful update restarts the app, so the window only closes on a failure.
        await new UpdateProgressWindow(updateClient, updateInfo).ShowDialog(this);
        ShowStatus(UpdateStatusText, "The update couldn't be installed. Please try again later.");
    }
}
