using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Folder-aware deck picker; search covers the open folder's subtree only, and folders with no decks on offer are hidden. </summary>
public partial class DeckFolderBrowser : ViewModelBase
{
    private const int FolderPriority = 0;
    private const int SetPriority = 1;

    private readonly FolderTree _tree = FolderTree.Load();
    private readonly Action<FlashCardDeck> _onDeckAction;
    private List<FlashCardDeck> _decks = [];
    private HashSet<ulong> _foldersWithDecks = [];
    private bool _suspendRefresh;

    /// <summary> Label on each deck's button, e.g. "Add" or "Upload". </summary>
    public string ActionText { get; }

    /// <summary> Narrow hosts (settings) drop the sort picker to leave room for the search box. </summary>
    public bool ShowSortOptions { get; init; } = true;

    public ObservableCollection<ISearchable> Items { get; } = [];
    public ObservableCollection<Folder> Breadcrumbs { get; } = [];

    public List<SortOption> SortOptions { get; } = SearchUtility.CreateSortOptions();

    [ObservableProperty] private SortOption? _selectedSortOption;
    partial void OnSelectedSortOptionChanged(SortOption? value) => Refresh();

    [ObservableProperty] private string _searchText = "";
    partial void OnSearchTextChanged(string value) => Refresh();

    [NotifyPropertyChangedFor(nameof(IsInFolder))]
    [NotifyPropertyChangedFor(nameof(CurrentFolderName))]
    [NotifyPropertyChangedFor(nameof(SearchWatermark))]
    [ObservableProperty] private ulong? _currentFolderID;

    public bool IsInFolder => CurrentFolderID.HasValue;
    public string CurrentFolderName => _tree.Get(CurrentFolderID)?.Name ?? FolderTree.RootLabel;

    public string SearchWatermark => IsInFolder
        ? $"Search in {CurrentFolderName} and its folders..."
        : "Search sets and folders...";

    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    [ObservableProperty] private string _emptyStateText = "";
    public bool HasNoItems => Items.Count == 0;

    public DeckFolderBrowser(string actionText, Action<FlashCardDeck> onDeckAction)
    {
        ActionText = actionText;
        _onDeckAction = onDeckAction;

        _suspendRefresh = true;
        SelectedSortOption = SortOptions[0];
        _suspendRefresh = false;
    }

    /// <summary> Replaces the decks on offer. Call again whenever the host's list changes. </summary>
    public void SetDecks(IEnumerable<FlashCardDeck> decks)
    {
        _decks = [.. decks];
        _tree.ApplyCounts(_decks, []);

        // A folder is worth showing only if a deck on offer sits somewhere beneath it.
        _foldersWithDecks = [];
        foreach (var deck in _decks)
        {
            foreach (var folder in _tree.AncestorChain(deck.FolderID)) _foldersWithDecks.Add(folder.ID);
        }

        // Folder cards should describe what opening them will actually show.
        foreach (var folder in _tree.AllFolders)
        {
            folder.SubFolderCount = _tree.ChildrenOf(folder.ID).Count(child => _foldersWithDecks.Contains(child.ID));
        }

        // The open folder may have just lost its last deck; step out to one that still has some.
        while (CurrentFolderID is ulong id && !_foldersWithDecks.Contains(id))
        {
            CurrentFolderID = _tree.Get(id)?.ParentFolderID;
        }

        RefreshFolderMetadata();
        Refresh();
    }

    // --- Navigation ---

    /// <summary> Opens a folder (or the main menu when null), clearing the search as it goes. </summary>
    public void NavigateTo(ulong? folderID)
    {
        if (!_tree.Exists(folderID)) folderID = null;

        _suspendRefresh = true;
        CurrentFolderID = folderID;
        SearchText = "";
        _suspendRefresh = false;

        RefreshFolderMetadata();
        Refresh();
    }

    [RelayCommand]
    private void OpenFolder(Folder folder) => NavigateTo(folder.ID);

    [RelayCommand]
    private void NavigateUp() => NavigateTo(_tree.Get(CurrentFolderID)?.ParentFolderID);

    /// <summary> Breadcrumb target: a folder, or null for the main menu crumb. </summary>
    [RelayCommand]
    private void NavigateToCrumb(Folder? folder) => NavigateTo(folder?.ID);

    [RelayCommand]
    private void DeckAction(FlashCardDeck deck) => _onDeckAction(deck);

    // --- Refresh ---

    private void RefreshFolderMetadata()
    {
        _tree.ApplyPaths(_decks, [], CurrentFolderID);

        Breadcrumbs.Clear();
        foreach (var folder in _tree.AncestorChain(CurrentFolderID)) Breadcrumbs.Add(folder);
    }

    private void Refresh()
    {
        if (_suspendRefresh) return;

        bool isSearching = !SearchUtility.IsEmptyQuery(SearchText);
        var subtree = CurrentFolderID is ulong currentId ? _tree.SubtreeIds(currentId) : null;
        bool InScope(ulong? folderID) => subtree is null || (folderID is ulong id && subtree.Contains(id));

        var visible = new List<ISearchable>();

        var folders = isSearching
            ? _tree.AllFolders.Where(folder => folder.ID != CurrentFolderID && InScope(folder.ParentFolderID))
            : _tree.ChildrenOf(CurrentFolderID);
        visible.AddRange(folders.Where(folder => _foldersWithDecks.Contains(folder.ID)));

        visible.AddRange(isSearching
            ? _decks.Where(deck => InScope(deck.FolderID))
            : _decks.Where(deck => deck.FolderID == CurrentFolderID));

        var ordered = visible.SearchAndSort(
            SearchText,
            SelectedSortOption?.Mode ?? SortMode.Relevance,
            item => item is Folder ? FolderPriority : SetPriority);

        Items.SyncTo(ordered);

        EmptyStateText =
            isSearching && IsInFolder ? $"Nothing in {CurrentFolderName} matches '{SearchText.Trim()}'." :
            isSearching ? $"Nothing matches '{SearchText.Trim()}'." :
            "No sets available.";

        OnPropertyChanged(nameof(HasNoItems));
    }
}
