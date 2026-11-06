using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;

namespace ReviFlash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        AddHandler(KeyDownEvent, Blocking_KeyDown, RoutingStrategies.Tunnel);
    }

    private bool _quitConfirmed;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        var page = (DataContext as DashboardViewModel)?.CurrentPage;

        // A note saves a moment after typing stops; closing the app in that moment shouldn't lose the last words.
        if (page is NoteViewModel note) note.SaveBeforeExit();

        // Colour edits are saved on leaving the themes page, which quitting skips.
        if (page is ThemePickerViewModel) MetaDataManager.SaveMetaData();

        if (page is DeckEditorViewModel editor)
        {
            editor.CommitDeckName();
            if (!_quitConfirmed && editor.HasUnsavedWork())
            {
                e.Cancel = true;
                ConfirmQuitDiscardingCard();
                return;
            }
            editor.Dispose();
        }
    }

    private async void ConfirmQuitDiscardingCard()
    {
        if (Main.DialogLayer.IsOpen) return;
        if (!await new ConfirmDialog("Discard Card?", "The card you're working on hasn't been saved.").ShowDialog<bool>(this)) return;

        _quitConfirmed = true;
        Close();
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        var cleanupStart = DateTime.Now;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var removed = ImageStore.RemoveUnused(cleanupStart);
                if (removed > 0) Logger.LogInfo($"Removed {removed} unused images.");
            }
            catch (Exception ex) { Logger.LogError("Removing unused images failed.", ex); }
        });

        if (ChangelogUtility.ShouldShowAfterUpdate(MetaDataManager.VersionBeforeLaunch, TextUtility.VersionText,
                MetaDataManager.Data.ShowChangelogAfterUpdate, ChangelogUtility.HasNotesFor(TextUtility.VersionNumber)))
            SyntaxGuideView.ShowFor(this, HelpTab.Changelog);

        if (!MetaDataManager.Data.CheckForUpdatesOnStartup) return;
        var updateClient = new UpdateClient();
        var check = await updateClient.CheckForUpdatesAsync();

        if (check.Info is { } updateInfo)
        {
            var confirmDialog = new ConfirmDialog("Update Available",
                $"Version {updateInfo.TargetFullRelease.Version} is ready. Download it and restart now?");

            bool confirmed = await confirmDialog.ShowDialog<bool>(this);

            if (confirmed)
            {
                await new UpdateProgressDialog(updateClient, updateInfo).ShowDialog(this);
            }
        }
    }

    /// <summary> Keys never reach the page behind an open dialog, and Tab doesn't move around the main menu, a review or its summary (the forms and notes use it). </summary>
    private void Blocking_KeyDown(object? sender, KeyEventArgs e)
    {
        if (Main.DialogLayer.IsOpen)
        {
            if (!Main.DialogLayer.IsInTopDialog(e.Source) && !Main.IsInHelp(e.Source)) e.Handled = true;
            return;
        }

        bool tabMovesFocus = e.Key == Key.Tab && (DataContext as DashboardViewModel)?.CurrentPage is DashboardViewModel or ReviewViewModel or SummaryViewModel;
        if (tabMovesFocus) e.Handled = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            SyntaxGuideView.ShowFor(this, HelpTab.KeyboardShortcuts);
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;
                SystemDecorations = SystemDecorations.Full;
            }
            else
            {
                WindowState = WindowState.FullScreen;
                SystemDecorations = SystemDecorations.None;
            }

            e.Handled = true;
        }
    }
}
