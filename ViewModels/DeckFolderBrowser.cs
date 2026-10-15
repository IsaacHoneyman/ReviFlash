using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Folder-aware set (or note) picker; search covers the open folder's subtree only, and folders with nothing on offer are hidden. </summary>
public partial class DeckFolderBrowser : FolderBrowserBase
{
    private readonly Action<LibraryItem> _onAction;
    private List<LibraryItem> _items = [];
    private HashSet<ulong> _foldersWithItems = [];
    private bool _showsNotes;

    /// <summary> Label on each deck's button, e.g. "Add" or "Upload". </summary>
    [ObservableProperty] private string _actionText;

    /// <summary> Narrow hosts (settings) drop the sort picker to leave room for the search box. </summary>
    public bool ShowSortOptions { get; init; } = true;

    /// <summary> Hosts turn the deck buttons off while their action is running. </summary>
    [NotifyCanExecuteChangedFor(nameof(DeckActionCommand))]
    [ObservableProperty] private bool _isActionEnabled = true;

    public ObservableCollection<ISearchable> Items { get; } = [];

    public override bool HasNoItems => Items.Count == 0;

    protected override string MainMenuSearchWatermark => _showsNotes ? "Search notes and folders..." : "Search sets and folders...";
    protected override string IdleEmptyStateText => _showsNotes ? "No notes available." : "No sets available.";

    public DeckFolderBrowser(string actionText, Action<FlashCardDeck> onDeckAction)
        : this(actionText, item => { if (item is FlashCardDeck deck) onDeckAction(deck); }) { }

    public DeckFolderBrowser(string actionText, Action<LibraryItem> onAction)
    {
        _actionText = actionText;
        _onAction = onAction;
        ReloadTree();
    }

    /// <summary> Replaces what's on offer with these sets. Call again whenever the host's list changes. </summary>
    public void SetDecks(IEnumerable<FlashCardDeck> decks) => SetItems(decks, showsNotes: false);

    /// <summary> Replaces what's on offer with these notes. </summary>
    public void SetNotes(IEnumerable<Note> notes) => SetItems(notes, showsNotes: true);

    private void SetItems(IEnumerable<LibraryItem> items, bool showsNotes)
    {
        // The host's change may have created folders too (a download that brought its folder path).
        ReloadTree();

        _showsNotes = showsNotes;
        _items = [.. items];
        Tree.ApplyCounts(_items.OfType<FlashCardDeck>(), [], _items.OfType<Note>());

        // A folder is worth showing only if something on offer sits somewhere beneath it.
        _foldersWithItems = [];
        foreach (var item in _items)
        {
            foreach (var folder in Tree.AncestorChain(item.ContainerFolderID)) _foldersWithItems.Add(folder.ID);
        }

        // Folder cards should describe what opening them will actually show.
        foreach (var folder in Tree.AllFolders)
        {
            folder.SubFolderCount = Tree.ChildrenOf(folder.ID).Count(child => _foldersWithItems.Contains(child.ID));
        }

        // The open folder may have just lost its last item; step out to one that still has some.
        while (CurrentFolderID is ulong id && !_foldersWithItems.Contains(id))
        {
            CurrentFolderID = Tree.Get(id)?.ParentFolderID;
        }

        OnPropertyChanged(nameof(SearchWatermark));
        RefreshFolderMetadata();
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(IsActionEnabled))]
    private void DeckAction(LibraryItem item) => _onAction(item);

    protected override void RefreshFolderMetadata()
    {
        Tree.ApplyPaths(_items, CurrentFolderID);
        base.RefreshFolderMetadata();
    }

    protected override IEnumerable<ISearchable> CollectItems() =>
        FoldersInScope().Where(folder => _foldersWithItems.Contains(folder.ID))
            .Concat<ISearchable>(InScope(_items));

    protected override void ShowItems(List<ISearchable> ordered) => Items.SyncTo(ordered);
}
