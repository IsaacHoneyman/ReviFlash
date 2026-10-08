using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Folder-aware deck picker; search covers the open folder's subtree only, and folders with no decks on offer are hidden. </summary>
public partial class DeckFolderBrowser : FolderBrowserBase
{
    private readonly Action<FlashCardDeck> _onDeckAction;
    private List<FlashCardDeck> _decks = [];
    private HashSet<ulong> _foldersWithDecks = [];

    /// <summary> Label on each deck's button, e.g. "Add" or "Upload". </summary>
    public string ActionText { get; }

    /// <summary> Narrow hosts (settings) drop the sort picker to leave room for the search box. </summary>
    public bool ShowSortOptions { get; init; } = true;

    public ObservableCollection<ISearchable> Items { get; } = [];

    public override bool HasNoItems => Items.Count == 0;

    protected override string MainMenuSearchWatermark => "Search sets and folders...";
    protected override string IdleEmptyStateText => "No sets available.";

    public DeckFolderBrowser(string actionText, Action<FlashCardDeck> onDeckAction)
    {
        ActionText = actionText;
        _onDeckAction = onDeckAction;
        ReloadTree();
    }

    /// <summary> Replaces the decks on offer. Call again whenever the host's list changes. </summary>
    public void SetDecks(IEnumerable<FlashCardDeck> decks)
    {
        // The host's change may have created folders too (a download that brought its folder path).
        ReloadTree();

        _decks = [.. decks];
        Tree.ApplyCounts(_decks, []);

        // A folder is worth showing only if a deck on offer sits somewhere beneath it.
        _foldersWithDecks = [];
        foreach (var deck in _decks)
        {
            foreach (var folder in Tree.AncestorChain(deck.FolderID)) _foldersWithDecks.Add(folder.ID);
        }

        // Folder cards should describe what opening them will actually show.
        foreach (var folder in Tree.AllFolders)
        {
            folder.SubFolderCount = Tree.ChildrenOf(folder.ID).Count(child => _foldersWithDecks.Contains(child.ID));
        }

        // The open folder may have just lost its last deck; step out to one that still has some.
        while (CurrentFolderID is ulong id && !_foldersWithDecks.Contains(id))
        {
            CurrentFolderID = Tree.Get(id)?.ParentFolderID;
        }

        RefreshFolderMetadata();
        Refresh();
    }

    [RelayCommand]
    private void DeckAction(FlashCardDeck deck) => _onDeckAction(deck);

    protected override void RefreshFolderMetadata()
    {
        Tree.ApplyPaths(_decks, CurrentFolderID);
        base.RefreshFolderMetadata();
    }

    protected override IEnumerable<ISearchable> CollectItems() =>
        FoldersInScope().Where(folder => _foldersWithDecks.Contains(folder.ID))
            .Concat<ISearchable>(InScope(_decks));

    protected override void ShowItems(List<ISearchable> ordered) => Items.SyncTo(ordered);
}
