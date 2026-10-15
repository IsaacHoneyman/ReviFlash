using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Community: search public sets or notes and download them. Only opened once signed in. </summary>
public partial class OnlineImportViewModel : OnlineViewModelBase
{
    /// <summary> Where downloads land, e.g. "Saving to Year 1 / Algorithms". </summary>
    public string TargetFolderText { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearching;

    /// <summary> Recreate the uploader's folders beneath the current folder. </summary>
    [ObservableProperty] private bool _includeFolderInfo;

    public ObservableCollection<FlashCardDeckMetadata> SearchResults { get; } = [];

    /// <summary> Searches public notes rather than sets. </summary>
    [NotifyPropertyChangedFor(nameof(ShowSets), nameof(IsNotesSelected), nameof(SearchWatermark))]
    [ObservableProperty] private bool _showNotes;
    partial void OnShowNotesChanged(bool value) => _ = SearchAsync();

    // Bound to toggle buttons, so clicking the selected one keeps it selected.
    public bool ShowSets
    {
        get => !ShowNotes;
        set { if (value) ShowNotes = false; else OnPropertyChanged(); }
    }

    public bool IsNotesSelected
    {
        get => ShowNotes;
        set { if (value) ShowNotes = true; else OnPropertyChanged(); }
    }

    public string SearchWatermark => ShowNotes ? "Search notes by title or uploader..." : "Search sets by title or uploader...";

    private CloudKind Kind => ShowNotes ? CloudKind.Notes : CloudKind.Sets;

    /// <summary> Cloud orderings, most downloaded first, applied to what the server returns. </summary>
    public List<SortOption> SortOptions { get; } = SearchUtility.CreateCloudSortOptions();

    [ObservableProperty] private SortOption? _selectedSortOption;
    partial void OnSelectedSortOptionChanged(SortOption? value) => ApplyOrdering();

    // The server matches title or uploader name; keep the raw results so re-sorting costs no round trip.
    private readonly List<FlashCardDeckMetadata> _rawResults = [];
    private string _rawResultsQuery = string.Empty;

    /// <summary> Folder downloaded sets are filed into; null for the main menu. </summary>
    private readonly ulong? _targetFolderID;

    public OnlineImportViewModel(ulong? targetFolderID = null)
    {
        _targetFolderID = targetFolderID;

        TargetFolderText = $"Saving to {FolderTree.Load().DisplayPath(targetFolderID)}";

        SelectedSortOption = SortOptions[0];
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsSearching = true;
        SearchResults.Clear();
        var kind = Kind;
        var noun = ShowNotes ? "note" : "set";

        await RunWithStatusAsync(ShowNotes ? "Searching public notes" : "Searching public sets", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var results = await client.GetPublicDecksAsync(SearchText, limit: 25, kind);

            // Switched between sets and notes while searching: that search has replaced this one.
            if (kind != Kind) return StatusMessage;

            _rawResults.Clear();
            _rawResults.AddRange(results);
            _rawResultsQuery = SearchText;
            ApplyOrdering();

            return SearchResults.Count == 0
                ? $"No {noun}s found matching your search."
                : $"Found {TextUtility.Plural(SearchResults.Count, noun)}.";
        }, "Search failed");

        if (kind == Kind) IsSearching = false;
    }

    [RelayCommand]
    private async Task DownloadAsync(FlashCardDeckMetadata? deck)
    {
        if (deck == null || string.IsNullOrWhiteSpace(deck.StoragePath))
        {
            StatusMessage = "Cannot download: Invalid deck or missing storage path.";
            return;
        }

        var kind = Kind;
        await RunWithStatusAsync($"Downloading '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath, kind);
            if (kind == CloudKind.Notes) DeckTransferManager.TryImportCloudNote(json, _targetFolderID, IncludeFolderInfo);
            else DeckTransferManager.TryImportCloudDeck(json, _targetFolderID, IncludeFolderInfo);

            // Counting is a bonus: it's already imported, so a failure here is only logged.
            if (await client.RecordDownloadAsync(deck.Id, kind) is int downloads) deck.DownloadCount = downloads;

            return kind == CloudKind.Notes
                ? $"Downloaded '{deck.Title}'. Open it from the main menu."
                : $"Successfully imported '{deck.Title}'! You can now review it.";
        }, "Import failed");
    }

    /// <summary> Ranks the server's results locally, in the same order a local search would produce. </summary>
    private void ApplyOrdering()
    {
        SearchResults.Clear();

        var ordered = _rawResults.SearchAndSort(_rawResultsQuery, SelectedSortOption?.Mode ?? SortMode.Relevance);
        foreach (var deck in ordered) SearchResults.Add(deck);
    }
}