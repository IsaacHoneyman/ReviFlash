using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

public partial class OnlineImportViewModel : ViewModelBase
{
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSearching;

    public ObservableCollection<FlashCardDeckMetadata> SearchResults { get; } = [];

    /// <summary> The same orderings the dashboard offers, applied to what the server returns. </summary>
    public List<SortOption> SortOptions { get; } = SearchUtility.CreateSortOptions();

    [ObservableProperty] private SortOption? _selectedSortOption;
    partial void OnSelectedSortOptionChanged(SortOption? value) => ApplyOrdering();

    // The server filters by title; keep the raw results so re-sorting costs no round trip.
    private readonly List<FlashCardDeckMetadata> _rawResults = [];

    /// <summary> Folder downloaded sets are filed into; null for the main menu. </summary>
    private readonly ulong? _targetFolderID;

    public OnlineImportViewModel(ulong? targetFolderID = null)
    {
        _targetFolderID = targetFolderID;
        SelectedSortOption = SortOptions[0];
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsSearching = true;
        SearchResults.Clear();

        using var cts = new CancellationTokenSource();
        var animationTask = AnimateStatusAsync("Searching public flashcards", cts.Token);

        try
        {
            using var client = new SupabaseConnection();
            var results = await client.GetPublicDecksAsync(SearchText, limit: 25);

            _rawResults.Clear();
            _rawResults.AddRange(results);
            ApplyOrdering();

            cts.Cancel();
            StatusMessage = SearchResults.Count == 0 ? "No decks found matching your search." : $"Found {SearchResults.Count} decks.";
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"Search failed: {ex.Message}";
        }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private async Task DownloadAsync(FlashCardDeckMetadata? deck)
    {
        if (deck == null || string.IsNullOrWhiteSpace(deck.StoragePath))
        {
            StatusMessage = "Cannot download: Invalid deck or missing storage path.";
            return;
        }

        using var cts = new CancellationTokenSource();
        var animationTask = AnimateStatusAsync($"Downloading '{deck.Title}'", cts.Token);

        try
        {
            using var client = new SupabaseConnection();
            string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath);            
            DeckTransferManager.TryImportCloudDeck(json, _targetFolderID);

            cts.Cancel();
            StatusMessage = $"Successfully imported '{deck.Title}'! You can now review it.";
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Ranks what the server returned locally, so cloud results come back in the same
    /// order a local search would produce.
    /// </summary>
    private void ApplyOrdering()
    {
        SearchResults.Clear();

        var ordered = _rawResults.SearchAndSort(SearchText, SelectedSortOption?.Mode ?? SortMode.Relevance);
        foreach (var deck in ordered) SearchResults.Add(deck);
    }

    private async Task AnimateStatusAsync(string baseMessage, CancellationToken token)
    {
        int dotCount = 1;
        while (!token.IsCancellationRequested)
        {
            StatusMessage = baseMessage + new string('.', dotCount);
            dotCount = (dotCount % 3) + 1;

            try { await Task.Delay(400, token); }
            catch (TaskCanceledException) { break; }
        }
    }
}