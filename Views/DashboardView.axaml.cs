using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using System.Collections.Generic;
using System.Linq;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;

namespace ReviFlash.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private Window OwnerWindow => (Window)TopLevel.GetTopLevel(this)!;

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel()
        };

        await settingsWindow.ShowDialog(OwnerWindow);
    }

    public async void CreateDeck_Click(object sender, RoutedEventArgs e)
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        FlashCardRepository.SaveNewDeck(newDeck);

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(newDeck)
        };
        await editor.ShowDialog(OwnerWindow);

        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
        }
    }

    public async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var editor = new StudyGroupEditorWindow
        {
            DataContext = new StudyGroupEditorViewModel()
        };

        await editor.ShowDialog(OwnerWindow);

        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadStudyGroupsFromDatabase();
        }
    }

    public async void EditDeck_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(selectedDeck)
        };
        await editor.ShowDialog(OwnerWindow);

        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
        }
    }

    public async void DeleteDeck_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));
        var dialog = new ConfirmDialogWindow($"Are you sure you want to permanently delete '{selectedDeck.Name}' and all of its cards?");

        bool confirmed = await dialog.ShowDialog<bool>(OwnerWindow);

        if (confirmed && DataContext is DashboardViewModel vm)
        {
            vm.DeleteDeck(selectedDeck);
        }
    }

    public void DeckStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var button = (Button)sender;
        var selectedDeck = (FlashCardDeck)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a FlashCardDeck"));

        if (DataContext is DashboardViewModel vm)
        {
            vm.ShowDeckStats(selectedDeck);
        }
    }

    public void GroupStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));

        if (DataContext is DashboardViewModel vm)
        {
            vm.ShowGroupStats(selectedGroup);
        }
    }

    public async void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));

        var editor = new StudyGroupEditorWindow
        {
            DataContext = new StudyGroupEditorViewModel(selectedGroup)
        };

        await editor.ShowDialog(OwnerWindow);

        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadStudyGroupsFromDatabase();
        }
    }

    public async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var selectedGroup = (StudyGroup)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a StudyGroup"));
        var dialog = new ConfirmDialogWindow($"Are you sure you want to permanently delete group '{selectedGroup.Name}'?");

        bool confirmed = await dialog.ShowDialog<bool>(OwnerWindow);

        if (confirmed && DataContext is DashboardViewModel vm)
        {
            vm.DeleteStudyGroup(selectedGroup);
        }
    }

    public void CloseDecKStats_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.ShowOverallStats();
        }
    }

    public void GraphStats_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.EnterGraphView();
        }
    }

    public void ExitGraphView_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.ExitGraphView();
        }
    }

    private void DeckCard_Click(object sender, PointerPressedEventArgs e)
    {
        // Ignore pointer events originating from action buttons inside the deck card.
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is FlashCardDeck deck)
        {
            if (DataContext is DashboardViewModel vm && vm.IsSelectionModeActive)
            {
                vm.ToggleDeckSelection(deck);
                return;
            }

            StartReviewSession(deck);
        }
    }

    private void DeckCard_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        // Ignore key events from nested action buttons.
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is FlashCardDeck deck)
        {
            if (DataContext is DashboardViewModel vm && vm.IsSelectionModeActive)
            {
                vm.ToggleDeckSelection(deck);
                e.Handled = true;
                return;
            }

            StartReviewSession(deck);
            e.Handled = true;
        }
    }

    private void GroupCard_Click(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (DataContext is DashboardViewModel vm && vm.IsSelectionModeActive)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is StudyGroup group)
        {
            StartReviewSession(FlashCardRepository.GetDecksForStudyGroup(group.ID), group.ID);
        }
    }

    private void GroupCard_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (DataContext is DashboardViewModel vm && vm.IsSelectionModeActive)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is StudyGroup group)
        {
            StartReviewSession(FlashCardRepository.GetDecksForStudyGroup(group.ID), group.ID);
            e.Handled = true;
        }
    }

    private void MultiSelectDecks_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        if (!vm.IsReviewSelectionMode)
        {
            vm.BeginReviewSelection();
            return;
        }

        if (!vm.HasSelectedDecks)
        {
            vm.CancelSelectionMode();
            return;
        }

        StartReviewSession(vm.GetSelectedDecks());
    }

    private async void ExportFlashCards_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        if (!vm.IsExportSelectionMode)
        {
            vm.BeginExportSelection();
            return;
        }

        if (!vm.HasSelectedDecks)
        {
            vm.CancelSelectionMode();
            return;
        }

        var saveFile = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export selected flashcard sets",
            SuggestedFileName = $"ReviFlashSets_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType("ReviFlash Export") { Patterns = ["*.zip"] }
            ]
        });

        if (saveFile is null)
        {
            return;
        }

        try
        {
            DeckTransferManager.TryCreateDeckExport(saveFile.Path.LocalPath, vm.GetSelectedDecks().Select(deck => deck.ID).ToList());
            vm.CancelSelectionMode();
        }
        catch (Exception ex)
        {
            Logger.LogError("Export failed", ex);
        }
    }

    private async void ImportFlashCards_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import flashcard sets",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ReviFlash Export") { Patterns = ["*.zip"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            DeckTransferManager.TryImportDeckExport(files[0].Path.LocalPath);
            vm.CancelSelectionMode();
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
            vm.RefreshStats();
        }
        catch (Exception ex)
        {
            Logger.LogError("Import failed", ex);
        }
    }

    private async void OpenOnlineImport_Click(object sender, RoutedEventArgs e)
    {
        var window = new OnlineImportWindow();
        await window.ShowDialog(OwnerWindow);

        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadDecksFromDatabase();
            vm.FilterDecks();
            vm.RefreshStats();
        }
    }

    private async void OpenOnlineExport_Click(object sender, RoutedEventArgs e)
    {
        var window = new OnlineExportWindow();
        await window.ShowDialog(OwnerWindow);
    }

    private void StartReviewSession(FlashCardDeck deck)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0) return;

        var reviewVM = new ReviewViewModel(cards, deck.ID)
        {
            // Capture vm directly rather than re-reading DataContext here: this callback
            // fires much later, by which point DashboardView has been swapped out of the
            // ContentControl for ReviewView, so `DataContext` on this instance is stale.
            OnSessionComplete = (score, total, time, isPartial) =>
                vm.CurrentPage = new SummaryViewModel(score, total, time, isPartial)
                {
                    OnReturnToDashboard = () => ReturnToDashboard(vm)
                }
        };

        vm.CurrentPage = reviewVM;
    }

    private void StartReviewSession(IReadOnlyList<FlashCardDeck> decks, ulong? groupId = null)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        var allCards = new List<FlashCard>();
        var cardDeckMap = new Dictionary<ulong, ulong>();

        foreach (var deck in decks)
        {
            var deckCards = FlashCardRepository.GetCardsForDeck(deck.ID);
            foreach (var card in deckCards)
            {
                allCards.Add(card);
                if (card.ID != ulong.MaxValue)
                {
                    cardDeckMap[card.ID] = deck.ID;
                }
            }
        }

        if (allCards.Count == 0)
        {
            return;
        }

        var reviewVM = new ReviewViewModel(allCards, ulong.MaxValue, cardDeckMap, groupId)
        {
            // See comment in the other StartReviewSession overload re: capturing vm directly.
            OnSessionComplete = (score, total, time, isPartial) =>
                vm.CurrentPage = new SummaryViewModel(score, total, time, isPartial)
                {
                    OnReturnToDashboard = () => ReturnToDashboard(vm)
                }
        };

        vm.CancelSelectionMode();
        vm.CurrentPage = reviewVM;
    }

    private static void ReturnToDashboard(DashboardViewModel vm)
    {
        vm.CurrentPage = vm; // Switches back to the Dashboard template
        vm.CancelSelectionMode();
        vm.LoadDecksFromDatabase();
        vm.FilterDecks();
        vm.RefreshStats(); // Refresh stats after session completes
    }
}
