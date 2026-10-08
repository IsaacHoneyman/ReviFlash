using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Cloud Manager: upload local decks and manage your own cloud decks. Only opened once signed in. </summary>
public partial class OnlineExportViewModel : OnlineViewModelBase
{
    /// <summary> Uploads carry the folder path as the cloud description, and downloads rebuild it. </summary>
    [ObservableProperty] private bool _includeFolderInfo;

    /// <summary> New uploads stay out of community search, visible only to their owner. </summary>
    [ObservableProperty] private bool _uploadAsPrivate;

    /// <summary> Folder your own decks are downloaded into; null for the main menu. </summary>
    private readonly ulong? _targetFolderID;

    [ObservableProperty] private bool _isSelectingUpdateDeck;
    [ObservableProperty] private FlashCardDeckMetadata? _targetCloudDeckToUpdate;
    [ObservableProperty] private FlashCardDeck? _selectedLocalDeckForUpdate;

    private string _cloudSearchText = string.Empty;
    public string CloudSearchText
    {
        get => _cloudSearchText;
        set { SetProperty(ref _cloudSearchText, value); RefreshCloudDecks(); }
    }

    /// <summary> Local decks to upload, browsed by folder like the main menu. </summary>
    public DeckFolderBrowser LocalBrowser { get; }

    /// <summary> Local deck picker inside the "update a cloud deck" overlay. </summary>
    public DeckFolderBrowser UpdateBrowser { get; }

    public ObservableCollection<FlashCardDeckMetadata> CloudDecks { get; } = [];
    public ObservableCollection<FlashCardDeckMetadata> FilteredCloudDecks { get; } = [];

    public List<SortOption> CloudSortOptions { get; } = SearchUtility.CreateCloudSortOptions();

    [ObservableProperty] private SortOption? _selectedCloudSortOption;
    partial void OnSelectedCloudSortOptionChanged(SortOption? value) => RefreshCloudDecks();

    /// <summary> Asks the user to confirm a destructive action; supplied by the window. </summary>
    public Func<string, Task<bool>>? ConfirmAsync { get; set; }

    public OnlineExportViewModel(ulong? targetFolderID = null)
    {
        _targetFolderID = targetFolderID;
        LocalBrowser = new DeckFolderBrowser("Upload", deck => UploadCommand.Execute(deck));
        UpdateBrowser = new DeckFolderBrowser("Select", deck => SelectedLocalDeckForUpdate = deck);
        SelectedCloudSortOption = CloudSortOptions[0];

        LocalBrowser.SetDecks(FlashCardRepository.GetAllDecks());
        _ = LoadCloudDecksAsync();
    }

    [RelayCommand]
    private void CancelUpdate()
    {
        IsSelectingUpdateDeck = false;
    }

    [RelayCommand]
    private void SetupUpdate(FlashCardDeckMetadata? deck)
    {
        if (deck == null) return;
        TargetCloudDeckToUpdate = deck;
        SelectedLocalDeckForUpdate = null;

        // Fresh deck objects: the upload pane stamps its own relative "in ..." labels on its copies.
        UpdateBrowser.NavigateTo(null);
        UpdateBrowser.SetDecks(FlashCardRepository.GetAllDecks());
        IsSelectingUpdateDeck = true;
    }

    [RelayCommand]
    private async Task UploadAsync(FlashCardDeck? deck)
    {
        if (deck == null || AuthSession.UserId is not { Length: > 0 } userId) return;

        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0)
        {
            StatusMessage = "Cannot upload an empty deck.";
            return;
        }

        await RunWithStatusAsync($"Uploading '{deck.Name}'", async () =>
        {
            var (jsonPayload, description) = BuildCloudPayload(deck);
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.UploadCloudDeckAsync(userId, deck.Name, description, cards.Count, jsonPayload, UploadAsPrivate);

            if (success) await LoadCloudDecksAsync();
            return message;
        }, "Upload failed");
    }

    [RelayCommand]
    private async Task ConfirmUpdateAsync()
    {
        if (TargetCloudDeckToUpdate is not { StoragePath: { } storagePath } target) return;

        if (SelectedLocalDeckForUpdate is not { } local)
        {
            StatusMessage = "Please select a local deck to upload.";
            return;
        }

        var cards = FlashCardRepository.GetCardsForDeck(local.ID);
        if (cards.Count == 0)
        {
            StatusMessage = "Cannot update with an empty local deck.";
            return;
        }

        IsSelectingUpdateDeck = false;

        await RunWithStatusAsync($"Updating '{target.Title}'", async () =>
        {
            var (jsonPayload, description) = BuildCloudPayload(local);
            using var client = await SupabaseConnection.CreateAsync();

            var (success, message) = await client.UpdateCloudDeckAsync(storagePath, local.Name, description, cards.Count, jsonPayload);

            if (success) await LoadCloudDecksAsync();
            return message;
        }, "Update failed");
    }

    [RelayCommand]
    private async Task ToggleVisibilityAsync(FlashCardDeckMetadata? deck)
    {
        if (deck?.StoragePath == null || AuthSession.UserId is not { Length: > 0 } userId) return;

        bool makePrivate = !deck.IsPrivate;
        if (makePrivate && ConfirmAsync is not null &&
            !await ConfirmAsync($"Make \"{deck.Title}\" private? It will disappear from community search. People who already downloaded it keep their copies."))
        {
            return;
        }

        await RunWithStatusAsync(makePrivate ? $"Making '{deck.Title}' private" : $"Making '{deck.Title}' public", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.SetCloudDeckVisibilityAsync(userId, deck.StoragePath, makePrivate);

            if (success) await LoadCloudDecksAsync();
            return message;
        }, "Change failed");
    }

    /// <summary> Brings one of your own cloud decks (public or private) onto this device. </summary>
    [RelayCommand]
    private async Task DownloadAsync(FlashCardDeckMetadata? deck)
    {
        if (deck?.StoragePath == null) return;

        await RunWithStatusAsync($"Downloading '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath);
            DeckTransferManager.TryImportCloudDeck(json, _targetFolderID, IncludeFolderInfo);

            // The new copy belongs in the upload pane too.
            LocalBrowser.SetDecks(FlashCardRepository.GetAllDecks());

            return $"Downloaded '{deck.Title}' to this device.";
        }, "Download failed");
    }

    [RelayCommand]
    private async Task DeleteAsync(FlashCardDeckMetadata? deck)
    {
        if (deck?.StoragePath == null) return;

        string downloads = deck.DownloadCount > 0 ? $" Its {deck.DownloadCountText} will be lost too." : "";
        if (ConfirmAsync is not null &&
            !await ConfirmAsync($"Delete \"{deck.Title}\" from the cloud? Anyone searching for it will no longer find it.{downloads} Your local copy is not affected."))
        {
            return;
        }

        await RunWithStatusAsync($"Deleting '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.DeleteCloudDeckAsync(deck.StoragePath);

            if (success) await LoadCloudDecksAsync();
            return message;
        }, "Delete failed");
    }

    /// <summary> The cloud JSON plus its description: the folder path when included, otherwise empty. </summary>
    private (string Json, string Description) BuildCloudPayload(FlashCardDeck deck)
    {
        List<string> folderPath = IncludeFolderInfo ? DeckTransferManager.GetFolderPathNames(deck.FolderID) : [];
        return (DeckTransferManager.GenerateCloudExportJson(deck.ID, folderPath), string.Join(FolderTree.PathSeparator, folderPath));
    }

    private void RefreshCloudDecks()
    {
        FilteredCloudDecks.Clear();

        var fDecks = CloudDecks.SearchAndSort(CloudSearchText, SelectedCloudSortOption?.Mode ?? SortMode.Relevance);
        foreach (var d in fDecks) FilteredCloudDecks.Add(d);
    }

    private async Task LoadCloudDecksAsync()
    {
        if (AuthSession.UserId is not { Length: > 0 } userId) return;

        using var client = await SupabaseConnection.CreateAsync();
        var remoteDecks = await client.GetUserCloudDecksAsync(userId);

        CloudDecks.Clear();
        foreach (var deck in remoteDecks) CloudDecks.Add(deck);

        RefreshCloudDecks();
    }
}