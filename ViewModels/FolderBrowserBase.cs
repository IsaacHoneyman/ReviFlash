using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Browses the library one folder at a time; searching covers the open folder's whole subtree, otherwise only what sits directly in it. </summary>
public abstract partial class FolderBrowserBase : ViewModelBase
{
    private bool _suspendRefresh;
    private HashSet<ulong>? _searchScope;

    protected FolderTree Tree { get; private set; } = new([]);

    public ObservableCollection<Folder> Breadcrumbs { get; } = [];

    public List<SortOption> SortOptions { get; } = SearchUtility.CreateSortOptions();

    [ObservableProperty] private SortOption? _selectedSortOption;
    partial void OnSelectedSortOptionChanged(SortOption? value) => Refresh();

    [NotifyPropertyChangedFor(nameof(IsSearching))]
    [ObservableProperty] private string _searchText = "";
    partial void OnSearchTextChanged(string value) => Refresh();

    /// <summary> The folder currently open, or null at the main menu. </summary>
    [NotifyPropertyChangedFor(nameof(IsInFolder))]
    [NotifyPropertyChangedFor(nameof(CurrentFolderName))]
    [NotifyPropertyChangedFor(nameof(SearchWatermark))]
    [ObservableProperty] private ulong? _currentFolderID;

    public bool IsInFolder => CurrentFolderID.HasValue;
    public string CurrentFolderName => Tree.Get(CurrentFolderID)?.Name ?? FolderTree.RootLabel;
    public bool IsSearching => !SearchUtility.IsEmptyQuery(SearchText);

    public string SearchWatermark => IsInFolder
        ? $"Search in {CurrentFolderName} and its folders..."
        : MainMenuSearchWatermark;

    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    [ObservableProperty] private string _emptyStateText = "";
    public abstract bool HasNoItems { get; }

    /// <summary> Breadcrumb target: a folder, or null for the main menu crumb. </summary>
    public IRelayCommand<Folder?> NavigateToFolderCommand => NavigateToCrumbCommand;

    protected FolderBrowserBase()
    {
        _suspendRefresh = true;
        SelectedSortOption = SortOptions[0];
        _suspendRefresh = false;
    }

    protected abstract string MainMenuSearchWatermark { get; }

    /// <summary> Shown when the open folder has nothing in it and no search is running. </summary>
    protected abstract string IdleEmptyStateText { get; }

    /// <summary> Everything that could be listed in the current scope, before searching and sorting; build it from <see cref="InScope{T}"/> and <see cref="FoldersInScope"/>. </summary>
    protected abstract IEnumerable<ISearchable> CollectItems();

    /// <summary> Puts the searched and sorted items on screen. </summary>
    protected abstract void ShowItems(List<ISearchable> ordered);

    /// <summary> Keeps a mixed list grouped by type outside relevance sorting. </summary>
    protected virtual int TypePriority(ISearchable item) => item is Folder ? 0 : 1;

    protected void ReloadTree() => Tree = FolderTree.Load();

    // --- Navigation ---

    /// <summary> Opens a folder (or the main menu when null, or when the folder is gone), clearing the search as it goes. </summary>
    public void NavigateTo(ulong? folderID)
    {
        if (!Tree.Exists(folderID)) folderID = null;

        SetLocation(folderID);
        RefreshFolderMetadata();
        Refresh();
    }

    [RelayCommand]
    public void OpenFolder(Folder folder) => NavigateTo(folder.ID);

    [RelayCommand]
    public void NavigateUp() => NavigateTo(Tree.Get(CurrentFolderID)?.ParentFolderID);

    [RelayCommand]
    private void NavigateToCrumb(Folder? folder) => NavigateTo(folder?.ID);

    /// <summary> Moves to a folder and clears the search without refreshing; the caller refreshes once afterwards. </summary>
    protected void SetLocation(ulong? folderID)
    {
        _suspendRefresh = true;
        CurrentFolderID = folderID;
        SearchText = "";
        _suspendRefresh = false;
    }

    // --- Scope ---

    protected IEnumerable<T> InScope<T>(IEnumerable<T> items) where T : LibraryItem =>
        items.Where(item => InScope(item.ContainerFolderID));

    protected IEnumerable<Folder> FoldersInScope() => IsSearching
        ? Tree.AllFolders.Where(folder => folder.ID != CurrentFolderID && InScope(folder.ParentFolderID))
        : Tree.ChildrenOf(CurrentFolderID);

    private bool InScope(ulong? folderID) => !IsSearching
        ? folderID == CurrentFolderID
        : _searchScope is null || (folderID is ulong id && _searchScope.Contains(id));

    // --- Refresh ---

    /// <summary> Breadcrumbs and folder labels: only reloading or navigating changes these, so searching skips them. </summary>
    protected virtual void RefreshFolderMetadata()
    {
        Breadcrumbs.Clear();
        foreach (var folder in Tree.AncestorChain(CurrentFolderID)) Breadcrumbs.Add(folder);

        OnPropertyChanged(nameof(IsInFolder));
        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(SearchWatermark));
    }

    /// <summary> Rebuilds the visible list from the open folder. </summary>
    protected void Refresh()
    {
        if (_suspendRefresh) return;

        // The open folder can disappear (deleted, or replaced by a restored backup); fall back to the main menu.
        if (!Tree.Exists(CurrentFolderID))
        {
            CurrentFolderID = null;
            RefreshFolderMetadata();
        }

        _searchScope = IsSearching && CurrentFolderID is ulong currentId ? Tree.SubtreeIds(currentId) : null;

        ShowItems(CollectItems().SearchAndSort(SearchText, SelectedSortOption?.Mode ?? SortMode.Relevance, TypePriority));

        EmptyStateText =
            IsSearching && IsInFolder ? $"Nothing in {CurrentFolderName} matches '{SearchText.Trim()}'." :
            IsSearching ? $"Nothing matches '{SearchText.Trim()}'." :
            IdleEmptyStateText;

        OnPropertyChanged(nameof(HasNoItems));
    }
}
