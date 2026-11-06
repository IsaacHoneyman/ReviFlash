using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;
using ReviFlash.Views.Controls;
using static ReviFlash.Utilities.TextUtility;

namespace ReviFlash.Views;

public partial class SettingsView : UserControl
{
    private TopLevel? _topLevel;

    public SettingsView()
    {
        InitializeComponent();
        Shortcuts.LeaveOnEscape(this, Leave);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // On the window, so the arrows work wherever the focus is.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, Sections_KeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, Sections_KeyDown);
        _topLevel = null;
    }

    /// <summary> Two panes: Up and Down change section in the list or move between settings in a section, Right steps into the section and Left back to the list. </summary>
    private void Sections_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || !Shortcuts.IsForPage(this, e)) return;
        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return;

        var focused = _topLevel?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or ComboBox) return;

        var settings = SectionControls();
        int inSection = focused is Control control ? settings.IndexOf(control) : -1;
        e.Handled = true;

        if (e.Key == Key.Left) FocusSectionList();
        else if (e.Key == Key.Right) settings.FirstOrDefault()?.Focus(NavigationMethod.Directional);
        else if (inSection >= 0)
        {
            int target = GridNavigation.Move(inSection, settings.Count, 1, e.Key == Key.Up ? GridMove.Up : GridMove.Down);
            if (target >= 0) settings[target].Focus(NavigationMethod.Directional);
        }
        else
        {
            int target = GridNavigation.Move(Sections.SelectedIndex, Sections.ItemCount, 1, e.Key == Key.Up ? GridMove.Up : GridMove.Down);
            if (target >= 0) Sections.SelectedIndex = target;
            FocusSectionList();
        }
    }

    private void FocusSectionList() => (Sections.ContainerFromIndex(Sections.SelectedIndex) as TabItem)?.Focus(NavigationMethod.Directional);

    /// <summary> The settings in the open section that take the keyboard, top to bottom. </summary>
    private List<Control> SectionControls() =>
        Sections.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(panel => panel.Classes.Contains("section") && panel.IsEffectivelyVisible)
            ?.GetVisualDescendants().OfType<Control>()
            .Where(c => c is CheckBox or Button && c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled)
            .ToList() ?? [];

    private DashboardViewModel? Dashboard => TopLevel.GetTopLevel(this)?.DataContext as DashboardViewModel;

    private IStorageProvider Storage => TopLevel.GetTopLevel(this)!.StorageProvider;

    private void Back_Click(object? sender, RoutedEventArgs e) => Leave();

    private void Leave() => (DataContext as SettingsViewModel)?.OnLeave?.Invoke();

    private static readonly TimeSpan SuccessFadeDelay = TimeSpan.FromSeconds(5);

    private static void ShowStatus(TextBlock statusText, string? message, bool fade = false)
    {
        statusText.Text = message;
        statusText.IsVisible = message is not null;
        if (fade && message is not null) _ = FadeStatusAsync(statusText, message);
    }

    private static async Task FadeStatusAsync(TextBlock statusText, string message)
    {
        await Task.Delay(SuccessFadeDelay);
        if (statusText.Text == message) ShowStatus(statusText, null);
    }

    private Task ShowErrorAsync(TextBlock statusText, string message)
    {
        ShowStatus(statusText, null);
        return ConfirmDialog.Notice("Something Went Wrong", message).ShowDialog(this);
    }

    private async void CreateBackup_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a Folder for the Backup",
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
            ShowStatus(BackupStatusText, "Backup created successfully.", fade: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(BackupStatusText, $"Backup failed: {ex.Message}");
        }
    }

    private async void ImportAnki_Click(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an Anki Deck",
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
            await ShowErrorAsync(AnkiImportStatusText, $"Import failed: {ex.Message}");
        }
        finally
        {
            AnkiImportButton.IsEnabled = true;
        }
    }

    private static string DescribeAnkiImport(AnkiImportResult result)
    {
        var message = $"Imported {Plural(result.Cards, "card")} into {Plural(result.Decks, "set")}";
        message += result.Images > 0 ? $", with {Plural(result.Images, "image")}." : ".";
        if (result.CardsWithMissingImages > 0)
            message += $" {Plural(result.CardsWithMissingImages, "card")} had images missing from the file or in a format ReviFlash can't read; they're marked [image] in {string.Join(", ", result.DecksWithMissingImages)}.";
        if (result.Skipped > 0)
            message += $" {Plural(result.Skipped, "note")} with nothing to ask but a missing image (or image occlusion) {(result.Skipped == 1 ? "was" : "were")} left out.";
        return message;
    }

    private async void RestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a Backup File",
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

        var confirmDialog = new ConfirmDialog("Restore Backup?",
            "Restoring replaces all of your current sets, notes, groups, folders and settings with the ones in the backup.");
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
            ShowStatus(BackupStatusText, "Backup restored successfully.", fade: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(BackupStatusText, $"Restore failed: {ex.Message}");
        }
    }

    private async void DeleteAllStats_Click(object? sender, RoutedEventArgs e)
    {
        var confirmDialog = new ConfirmDialog("Delete All Stats?",
            "This clears every set's results, study and editing time, notes time and streaks. Your sets and notes are not affected.");

        bool confirmed = await confirmDialog.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        FlashCardRepository.DeleteAllStats();
        MetaDataManager.Data.LaunchStreak = 0;
        MetaDataManager.Data.BestLaunchStreak = 0;
        MetaDataManager.SaveMetaData();
        Dashboard?.ReloadAfterEdit();
        ShowStatus(DeleteStatsStatusText, "All stats were deleted.", fade: true);
    }

    /// <summary> Opens the themes as a page over this one; coming back shows the theme now in use. </summary>
    private void ChooseTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (Dashboard is not { } dashboard || DataContext is not SettingsViewModel settings) return;

        var themes = new ThemePickerViewModel();
        themes.OnLeave = () =>
        {
            dashboard.GoBack();
            settings.RefreshFromMetadata();
        };
        dashboard.OpenPage(themes);
    }

    private async void OpenDeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new DeleteDeckStatsDialog();
        dialog.StatsDeleted += () => Dashboard?.ReloadAfterEdit();
        await dialog.ShowDialog(this);
    }

    // --- Account ---

    private async void SignIn_Click(object? sender, RoutedEventArgs e)
    {
        // The account section updates itself from the stored session.
        await new LoginDialog().ShowDialog<bool>(this);
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

        var prompt = new TextPromptDialog("Change Username", "Save", AuthSession.Username,
            $"{min}-{max} characters. Shown on the sets and notes you share.")
        {
            Validate = LoginViewModel.UsernameProblem,
        };
        var username = await prompt.ShowDialog<string?>(this);
        if (username is null || username == AuthSession.Username) return;

        ShowStatus(AccountStatusText, "Saving username...");
        var result = await RunBusyAsync(sender, AccountSpinner, () => AuthSession.ChangeUsernameAsync(username));
        if (result.Success) ShowStatus(AccountStatusText, $"Username changed to {username}.", fade: true);
        else await ShowErrorAsync(AccountStatusText, $"Couldn't change username: {result.Message}");
    }

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
    {
        ShowStatus(UpdateStatusText, "Checking for updates...");

        var updateClient = new UpdateClient();
        var check = await RunBusyAsync(sender, UpdateSpinner, updateClient.CheckForUpdatesAsync);

        if (check.Status == UpdateCheckStatus.Failed)
        {
            await ShowErrorAsync(UpdateStatusText, "Couldn't check for updates. Check your connection.");
            return;
        }

        if (check.Info is not { } updateInfo)
        {
            ShowStatus(UpdateStatusText, "You are already on the latest version.", fade: true);
            return;
        }

        var version = updateInfo.TargetFullRelease.Version;
        ShowStatus(UpdateStatusText, $"Version {version} available!");
        var confirmDialog = new ConfirmDialog("Update Available", $"Version {version} is ready. Download it and restart now?");

        if (!await confirmDialog.ShowDialog<bool>(this))
        {
            ShowStatus(UpdateStatusText, "Update cancelled.", fade: true);
            return;
        }

        // A successful update restarts the app, so the dialog only closes on a failure.
        await new UpdateProgressDialog(updateClient, updateInfo).ShowDialog(this);
        await ShowErrorAsync(UpdateStatusText, "The update couldn't be installed. Please try again later.");
    }
}
