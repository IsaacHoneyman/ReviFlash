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
using ReviFlash.Utilities;
using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

public partial class DashboardView : UserControl
{
    /// <summary> Below this width the library column uses short button labels and a narrower sort box. </summary>
    private const double CompactLibraryWidth = 790;

    public DashboardView()
    {
        InitializeComponent();
        LibraryColumn.SizeChanged += (_, e) => LibraryColumn.Classes.Set("compact", e.NewSize.Width < CompactLibraryWidth);
    }

    private Window OwnerWindow => (Window)TopLevel.GetTopLevel(this)!;

    /// <summary> The library item a card or card button belongs to. </summary>
    private static T ItemOf<T>(object sender) where T : class =>
        (sender as Control)?.DataContext as T ?? throw new InvalidOperationException($"Control's DataContext is not a {typeof(T).Name}");

    private static string CreationHint(DashboardViewModel vm) => vm.IsInFolder
        ? $"It will be created inside {vm.FolderTree.DisplayPath(vm.CurrentFolderID)}."
        : "It will be created on the main menu.";

    private void HelpButton_Click(object sender, RoutedEventArgs e) => SyntaxGuideWindow.ShowFor(OwnerWindow);

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

    // --- Notes ---

    public async void CreateNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var prompt = new TextPromptWindow(
            "Name your new note",
            "Create",
            "New Note",
            CreationHint(vm));

        var name = await prompt.ShowDialog<string?>(OwnerWindow);
        if (string.IsNullOrWhiteSpace(name)) return;

        vm.OpenNote(vm.CreateNote(name));
    }

    public async void RenameNote_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var note = ItemOf<Note>(sender);
        if (DataContext is not DashboardViewModel vm) return;

        var name = await new TextPromptWindow("Rename note", "Rename", note.Name).ShowDialog<string?>(OwnerWindow);
        if (string.IsNullOrWhiteSpace(name) || name == note.Name) return;

        vm.RenameNote(note, name);
    }

    public void NoteStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is DashboardViewModel vm) vm.ShowNoteStats(ItemOf<Note>(sender));
    }

    public async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var note = ItemOf<Note>(sender);
        if (DataContext is not DashboardViewModel vm) return;

        var dialog = new ConfirmDialogWindow($"Are you sure you want to permanently delete the note '{note.Name}'?");
        if (await dialog.ShowDialog<bool>(OwnerWindow)) vm.DeleteNote(note);
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
            CreationHint(vm));

        var name = await prompt.ShowDialog<string?>(OwnerWindow);
        if (string.IsNullOrWhiteSpace(name)) return;

        vm.CreateFolder(name);
    }

    public async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var folder = ItemOf<Folder>(sender);

        if (DataContext is not DashboardViewModel vm) return;

        var prompt = new TextPromptWindow("Rename folder", "Rename", folder.Name);
        var name = await prompt.ShowDialog<string?>(OwnerWindow);

        if (string.IsNullOrWhiteSpace(name) || name == folder.Name) return;

        vm.RenameFolder(folder, name);
    }

    public async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var folder = ItemOf<Folder>(sender);

        if (DataContext is not DashboardViewModel vm) return;

        // Deleting a folder never deletes what is inside it, so say so plainly.
        string destination = vm.FolderTree.Get(folder.ParentFolderID)?.Name ?? "the main menu";
        string message = folder.ItemCount == 0
            ? $"Delete the empty folder '{folder.Name}'?"
            : $"Delete the folder '{folder.Name}'? Its {folder.ContentsSummary.ToLowerInvariant()} will be moved to {destination}. No flashcards are deleted.";

        if (folder.ItemCount == 0)
        {
            if (await new ConfirmDialogWindow(message).ShowDialog<bool>(OwnerWindow)) vm.DeleteFolder(folder);
            return;
        }

        var (folders, sets, groups, notes, cards) = FolderRepository.CountContents(vm.FolderTree.SubtreeIds(folder.ID));
        var everything = string.Join(", ", new[] { (folders, "folder"), (sets, "set"), (notes, "note"), (groups, "group") }
            .Where(part => part.Item1 > 0)
            .Select(part => TextUtility.Plural(part.Item1, part.Item2)));
        var deleteMessage = $"Delete the folder '{folder.Name}' and everything in it ({everything}, {TextUtility.Plural(cards, "card")})? This can't be undone.";

        var dialog = new ConfirmDialogWindow(message, "Also delete everything inside", deleteMessage);
        if (await dialog.ShowDialog<bool>(OwnerWindow)) vm.DeleteFolder(folder, withContents: dialog.IsOptionChecked);
    }

    public async void MoveItem_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var item = ItemOf<LibraryItem>(sender);
        if (DataContext is not DashboardViewModel vm) return;

        var picker = new MoveToFolderWindow
        {
            DataContext = new MoveToFolderViewModel(vm.FolderTree, item, item.ContainerFolderID)
        };

        var choice = await picker.ShowDialog<FolderChoice?>(OwnerWindow);
        if (choice is null) return;

        if (!vm.MoveItem(item, choice.FolderID))
        {
            var failed = new ConfirmDialogWindow("That move is not possible: a folder cannot be placed inside itself.");
            await failed.ShowDialog<bool>(OwnerWindow);
        }
    }

    private void Breadcrumbs_CrumbRequested(object? sender, CrumbRequestedEventArgs e) =>
        (DataContext as DashboardViewModel)?.NavigateToFolder(e.Folder?.ID);

    private void Breadcrumbs_UpRequested(object? sender, RoutedEventArgs e) =>
        (DataContext as DashboardViewModel)?.NavigateUp();

    public async void EditDeck_Click(object sender, RoutedEventArgs e)
    {
        var selectedDeck = ItemOf<FlashCardDeck>(sender);

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
        var selectedDeck = ItemOf<FlashCardDeck>(sender);
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
        var selectedDeck = ItemOf<FlashCardDeck>(sender);

        if (DataContext is DashboardViewModel vm)
        {
            vm.ShowDeckStats(selectedDeck);
        }
    }

    public void GroupStats_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var selectedGroup = ItemOf<StudyGroup>(sender);

        if (DataContext is DashboardViewModel vm)
        {
            vm.ShowGroupStats(selectedGroup);
        }
    }

    public async void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        var selectedGroup = ItemOf<StudyGroup>(sender);

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
        var selectedGroup = ItemOf<StudyGroup>(sender);
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

    private void Card_Click(object sender, PointerPressedEventArgs e)
    {
        if (!IsFromCardButton(e)) OpenCard(sender);
    }

    private void Card_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is (Key.Enter or Key.Space) && !IsFromCardButton(e)) e.Handled = OpenCard(sender);
    }

    private static bool IsFromCardButton(RoutedEventArgs e) =>
        e.Source is Control source && source.FindAncestorOfType<Button>() is not null;

    /// <summary> Opens a folder or note, reviews a set or group, or toggles a set while picking sets to review. Returns whether it acted. </summary>
    private bool OpenCard(object sender)
    {
        if (DataContext is not DashboardViewModel vm) return false;

        switch (ItemOf<LibraryItem>(sender))
        {
            case Folder folder:
                vm.OpenFolder(folder);
                return true;

            case FlashCardDeck deck when vm.IsSelectionModeActive:
                vm.ToggleDeckSelection(deck);
                return true;

            case FlashCardDeck deck:
                StartReviewSession(deck);
                return true;

            case StudyGroup group when !vm.IsSelectionModeActive:
                StartReviewSession(FlashCardRepository.GetDecksForStudyGroup(group.ID), group.ID);
                return true;

            case Note note when !vm.IsSelectionModeActive:
                vm.OpenNote(note);
                return true;

            default:
                return false;
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
        if (DataContext is not DashboardViewModel vm) return;

        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0) return;

        ShowReview(vm, new ReviewViewModel(cards, deck.ID));
    }

    private void StartReviewSession(IReadOnlyList<FlashCardDeck> decks, ulong? groupId = null)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var allCards = new List<FlashCard>();
        var cardDeckMap = new Dictionary<ulong, ulong>();

        foreach (var deck in decks)
        {
            foreach (var card in FlashCardRepository.GetCardsForDeck(deck.ID))
            {
                allCards.Add(card);
                if (card.ID != ulong.MaxValue) cardDeckMap[card.ID] = deck.ID;
            }
        }

        if (allCards.Count == 0) return;

        var reviewVM = new ReviewViewModel(allCards, ulong.MaxValue, cardDeckMap, groupId);
        vm.CancelSelectionMode();
        ShowReview(vm, reviewVM);
    }

    private static void ShowReview(DashboardViewModel vm, ReviewViewModel reviewVM)
    {
        // Capture vm: by the time this runs the view has been swapped for ReviewView, so its DataContext is stale.
        reviewVM.OnSessionComplete = (score, total, time, isPartial) =>
            vm.CurrentPage = new SummaryViewModel(score, total, time, isPartial)
            {
                OnReturnToDashboard = () => ReturnToDashboard(vm)
            };

        vm.CurrentPage = reviewVM;
    }

    private static void ReturnToDashboard(DashboardViewModel vm)
    {
        vm.CurrentPage = vm; // Switches back to the Dashboard template
        vm.CancelSelectionMode();
        vm.ReloadLibrary();
        vm.RefreshStats();
    }
}
