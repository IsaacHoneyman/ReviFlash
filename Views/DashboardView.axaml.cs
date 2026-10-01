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
using System.Threading.Tasks;
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
        if (DataContext is not DashboardViewModel vm) return;

        var newDeck = vm.CreateNewDeck();

        var editor = new DeckEditorWindow
        {
            DataContext = new DeckEditorViewModel(newDeck)
        };
        await editor.ShowDialog(OwnerWindow);

        vm.ReloadLibrary();
    }

    public async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var editor = new StudyGroupEditorWindow
        {
            DataContext = new StudyGroupEditorViewModel(targetFolderID: vm.CurrentFolderID)
        };

        await editor.ShowDialog(OwnerWindow);

        vm.ReloadLibrary();
    }

    // --- Folders ---

    public async void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var prompt = new TextPromptWindow(
            "Name your new folder",
            "Create",
            "New Folder",
            vm.IsInFolder ? $"It will be created inside {vm.CurrentFolderName}." : "It will be created on the main menu.");

        var name = await prompt.ShowDialog<string?>(OwnerWindow);
        if (string.IsNullOrWhiteSpace(name)) return;

        vm.CreateFolder(name);
    }

    public async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var button = (Button)sender;
        var folder = (Folder)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a Folder"));

        if (DataContext is not DashboardViewModel vm) return;

        var prompt = new TextPromptWindow("Rename folder", "Rename", folder.Name);
        var name = await prompt.ShowDialog<string?>(OwnerWindow);

        if (string.IsNullOrWhiteSpace(name) || name == folder.Name) return;

        vm.RenameFolder(folder, name);
    }

    public async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var button = (Button)sender;
        var folder = (Folder)(button.DataContext ?? throw new InvalidOperationException("Button's DataContext is not a Folder"));

        if (DataContext is not DashboardViewModel vm) return;

        // Deleting a folder never deletes what is inside it, so say so plainly.
        string destination = vm.FolderTree.Get(folder.ParentFolderID)?.Name ?? "the main menu";
        string message = folder.ItemCount == 0
            ? $"Delete the empty folder '{folder.Name}'?"
            : $"Delete the folder '{folder.Name}'? Its {folder.ContentsSummary.ToLowerInvariant()} will be moved to {destination}. No flashcards are deleted.";

        var dialog = new ConfirmDialogWindow(message);
        bool confirmed = await dialog.ShowDialog<bool>(OwnerWindow);

        if (confirmed) vm.DeleteFolder(folder);
    }

    public async void MoveItem_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var button = (Button)sender;
        var item = button.DataContext ?? throw new InvalidOperationException("Button has no DataContext to move");

        if (DataContext is not DashboardViewModel vm) return;

        ulong? currentFolderID = item switch
        {
            Folder folder => folder.ParentFolderID,
            StudyGroup group => group.FolderID,
            FlashCardDeck deck => deck.FolderID,
            _ => null,
        };

        var picker = new MoveToFolderWindow
        {
            DataContext = new MoveToFolderViewModel(vm.FolderTree, item, currentFolderID)
        };

        var choice = await picker.ShowDialog<FolderChoice?>(OwnerWindow);
        if (choice is null) return;

        if (!vm.MoveItem(item, choice.FolderID))
        {
            var failed = new ConfirmDialogWindow("That move is not possible: a folder cannot be placed inside itself.");
            await failed.ShowDialog<bool>(OwnerWindow);
        }
    }

    private void FolderCard_Click(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is Folder folder && DataContext is DashboardViewModel vm)
        {
            vm.OpenFolder(folder);
        }
    }

    private void FolderCard_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var border = (Border)sender;
        if (border.DataContext is Folder folder && DataContext is DashboardViewModel vm)
        {
            vm.OpenFolder(folder);
            e.Handled = true;
        }
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        if (DataContext is DashboardViewModel vm)
        {
            // The main menu crumb carries no folder; everything else carries its folder.
            vm.NavigateToFolder((button.DataContext as Folder)?.ID);
        }
    }

    private void NavigateUp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.NavigateUp();
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
            vm.ReloadLibrary();
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
            vm.ReloadLibrary();
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

    /// <summary> True once there is a ReviFlash Online session, asking the user to sign in if needed. </summary>
    private async Task<bool> EnsureSignedInAsync()
    {
        if (await AuthSession.TryRestoreAsync()) return true;
        return await new LoginWindow().ShowDialog<bool>(OwnerWindow);
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        // The account text updates itself from the stored session; nothing else to do.
        await EnsureSignedInAsync();
    }

    private async void OpenOnlineImport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;
        if (!await EnsureSignedInAsync()) return;

        var window = new OnlineImportWindow(vm.CurrentFolderID);
        await window.ShowDialog(OwnerWindow);

        vm.ReloadLibrary();
        vm.RefreshStats();
    }

    private async void OpenOnlineExport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;
        if (!await EnsureSignedInAsync()) return;

        // Your own decks can be downloaded from here, into the folder you're in.
        var window = new OnlineExportWindow(vm.CurrentFolderID);
        await window.ShowDialog(OwnerWindow);

        vm.ReloadLibrary();
        vm.RefreshStats();
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
        vm.ReloadLibrary();
        vm.RefreshStats(); // Refresh stats after session completes
    }
}
