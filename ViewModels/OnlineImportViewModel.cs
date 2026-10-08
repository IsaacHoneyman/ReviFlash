using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Community Flashcards: search public decks and download them. Only opened once signed in. </summary>
public partial class OnlineImportViewModel : OnlineViewModelBase
{
    /// <summary> Where downloads land, e.g. "Saving to Year 1 / Algorithms". </summary>
    public string TargetFolderText { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearching;

    /// <summary> Recreate the uploader's folders beneath the current folder. </summary>
    [ObservableProperty] private bool _includeFolderInfo;

    public ObservableCollection<FlashCardDeckMetadata> SearchResults { get; } = [];

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

        await RunWithStatusAsync("Searching public flashcards", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var results = await client.GetPublicDecksAsync(SearchText, limit: 25);

            _rawResults.Clear();
            _rawResults.AddRange(results);
            _rawResultsQuery = SearchText;
            ApplyOrdering();

            return SearchResults.Count == 0
                ? "No decks found matching your search."
                : $"Found {TextUtility.Plural(SearchResults.Count, "deck")}.";
        }, "Search failed");

        IsSearching = false;
    }

    [RelayCommand]
    private async Task DownloadAsync(FlashCardDeckMetadata? deck)
    {
        if (deck == null || string.IsNullOrWhiteSpace(deck.StoragePath))
        {
            StatusMessage = "Cannot download: Invalid deck or missing storage path.";
            return;
        }

        await RunWithStatusAsync($"Downloading '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath);
            DeckTransferManager.TryImportCloudDeck(json, _targetFolderID, IncludeFolderInfo);

            // Counting is a bonus: the deck is already imported, so a failure here is only logged.
            if (await client.RecordDownloadAsync(deck.Id) is int downloads) deck.DownloadCount = downloads;

            return $"Successfully imported '{deck.Title}'! You can now review it.";
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