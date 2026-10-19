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

/// <summary> Cloud Manager: upload local sets or notes and manage your own cloud copies. Only opened once signed in. </summary>
public partial class OnlineExportViewModel : OnlineViewModelBase
{
    /// <summary> Uploads carry the folder path as the cloud description, and downloads rebuild it. </summary>
    [ObservableProperty] private bool _includeFolderInfo;

    /// <summary> New uploads stay out of community search, visible only to their owner. </summary>
    [ObservableProperty] private bool _uploadAsPrivate;

    /// <summary> Folder your own sets and notes are downloaded into; null for the main menu. </summary>
    private readonly ulong? _targetFolderID;

    [ObservableProperty] private bool _isSelectingUpdateDeck;
    [ObservableProperty] private FlashCardDeckMetadata? _targetCloudDeckToUpdate;
    [ObservableProperty] private LibraryItem? _selectedLocalItemForUpdate;

    /// <summary> Both panes show notes rather than sets. </summary>
    [NotifyPropertyChangedFor(nameof(ItemNoun), nameof(ItemsNoun), nameof(CloudSearchWatermark),
        nameof(LoadingText), nameof(UpdateTitle), nameof(UpdateHint))]
    [ObservableProperty] private bool _showNotes;
    partial void OnShowNotesChanged(bool value)
    {
        IsSelectingUpdateDeck = false;
        CloudSearchText = "";
        ShowLocalItems();
        CloudDecks.Clear();
        FilteredCloudDecks.Clear();
        _ = LoadInitialCloudDecksAsync();
    }

    // Bound to toggle buttons, so clicking the selected one keeps it selected.


    private CloudKind Kind => ShowNotes ? CloudKind.Notes : CloudKind.Sets;

    public string ItemNoun => ShowNotes ? "note" : "set";
    public string ItemsNoun => ShowNotes ? "notes" : "sets";
    public string CloudSearchWatermark => $"Search your cloud {ItemsNoun}...";
    public string LoadingText => $"Loading your cloud {ItemsNoun}...";
    public string UpdateTitle => ShowNotes ? "Select Local Note" : "Select Local Set";
    public string UpdateHint => $"Choose which {ItemNoun} on this device should replace the one in the cloud.";

    private string _cloudSearchText = string.Empty;
    public string CloudSearchText
    {
        get => _cloudSearchText;
        set { SetProperty(ref _cloudSearchText, value); RefreshCloudDecks(); }
    }

    /// <summary> Local sets or notes to upload, browsed by folder like the main menu. </summary>
    public DeckFolderBrowser LocalBrowser { get; }

    /// <summary> Local picker inside the "update a cloud copy" overlay. </summary>
    public DeckFolderBrowser UpdateBrowser { get; }

    public ObservableCollection<FlashCardDeckMetadata> CloudDecks { get; } = [];
    public ObservableCollection<FlashCardDeckMetadata> FilteredCloudDecks { get; } = [];

    public List<SortOption> CloudSortOptions { get; } = SearchUtility.CreateCloudSortOptions();

    [ObservableProperty] private SortOption? _selectedCloudSortOption;
    partial void OnSelectedCloudSortOptionChanged(SortOption? value) => RefreshCloudDecks();

    /// <summary> True while an upload, update, visibility change, download or delete runs. </summary>
    [NotifyCanExecuteChangedFor(nameof(UploadCommand), nameof(ConfirmUpdateCommand), nameof(ToggleVisibilityCommand), nameof(DownloadCommand), nameof(DeleteCommand))]
    [ObservableProperty] private bool _isBusy;

    private bool CanAct() => !IsBusy;

    /// <summary> True until the first fetch of your cloud copies finishes. </summary>
    [ObservableProperty] private bool _isLoadingCloudDecks;

    partial void OnIsBusyChanged(bool value)
    {
        LocalBrowser.IsActionEnabled = !value;
        UpdateBrowser.IsActionEnabled = !value;
    }

    /// <summary> Asks the user to confirm a destructive action; supplied by the window. </summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

    public OnlineExportViewModel(ulong? targetFolderID = null)
    {
        _targetFolderID = targetFolderID;
        LocalBrowser = new DeckFolderBrowser("Upload", (LibraryItem item) => UploadCommand.Execute(item));
        UpdateBrowser = new DeckFolderBrowser("Select", (LibraryItem item) => SelectedLocalItemForUpdate = item);
        SelectedCloudSortOption = CloudSortOptions[0];

        ShowLocalItems();
        _ = LoadInitialCloudDecksAsync();
    }

    private void ShowLocalItems() => ShowLocalItems(LocalBrowser);

    private void ShowLocalItems(DeckFolderBrowser browser)
    {
        if (ShowNotes) browser.SetNotes(NoteRepository.GetAllNotes());
        else browser.SetDecks(FlashCardRepository.GetAllDecks());
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
        SelectedLocalItemForUpdate = null;

        // Fresh objects: the upload pane stamps its own relative "in ..." labels on its copies.
        UpdateBrowser.NavigateTo(null);
        ShowLocalItems(UpdateBrowser);
        IsSelectingUpdateDeck = true;
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task UploadAsync(LibraryItem? item)
    {
        if (IsBusy || item == null || AuthSession.UserId is not { Length: > 0 } userId) return;

        if (PrepareUpload(item) is not { } upload) return;
        var kind = Kind;

        await RunGuardedAsync($"Uploading '{item.Name}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.UploadCloudDeckAsync(userId, item.Name, upload.Description, upload.Size, upload.Json, UploadAsPrivate, kind);

            return await AfterCloudChangeAsync(success, message);
        }, "Upload failed");
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task ConfirmUpdateAsync()
    {
        if (IsBusy || TargetCloudDeckToUpdate is not { StoragePath: { } storagePath } target) return;

        if (SelectedLocalItemForUpdate is not { } local)
        {
            ReportFailure($"Please select a {ItemNoun} on this device to upload.");
            return;
        }

        if (PrepareUpload(local) is not { } upload) return;
        var kind = Kind;

        IsSelectingUpdateDeck = false;

        await RunGuardedAsync($"Updating '{target.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.UpdateCloudDeckAsync(storagePath, local.Name, upload.Description, upload.Size, upload.Json, kind);

            return await AfterCloudChangeAsync(success, message);
        }, "Update failed");
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task ToggleVisibilityAsync(FlashCardDeckMetadata? deck)
    {
        if (IsBusy || deck?.StoragePath == null || AuthSession.UserId is not { Length: > 0 } userId) return;

        bool makePrivate = !deck.IsPrivate;
        if (makePrivate && ConfirmAsync is not null &&
            !await ConfirmAsync("Make Private?", $"\"{deck.Title}\" will disappear from community search. People who already downloaded it keep their copies."))
        {
            return;
        }

        var kind = Kind;
        await RunGuardedAsync(makePrivate ? $"Making '{deck.Title}' private" : $"Making '{deck.Title}' public", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.SetCloudDeckVisibilityAsync(userId, deck.StoragePath, makePrivate, kind);

            return await AfterCloudChangeAsync(success, message);
        }, "Change failed");
    }

    /// <summary> Brings one of your own cloud sets or notes (public or private) onto this device. </summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task DownloadAsync(FlashCardDeckMetadata? deck)
    {
        if (IsBusy || deck?.StoragePath == null) return;

        var kind = Kind;
        await RunGuardedAsync($"Downloading '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath, kind);
            if (kind == CloudKind.Notes) DeckTransferManager.TryImportCloudNote(json, _targetFolderID, IncludeFolderInfo);
            else DeckTransferManager.TryImportCloudDeck(json, _targetFolderID, IncludeFolderInfo);

            // The new copy belongs in the upload pane too.
            if (kind == Kind) ShowLocalItems();

            return $"Downloaded '{deck.Title}' to this device.";
        }, "Download failed");
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task DeleteAsync(FlashCardDeckMetadata? deck)
    {
        if (IsBusy || deck?.StoragePath == null) return;

        string downloads = deck.DownloadCount > 0 ? $" Its {deck.DownloadCountText} will be lost too." : "";
        if (ConfirmAsync is not null &&
            !await ConfirmAsync("Delete from the Cloud?", $"Anyone searching for \"{deck.Title}\" will no longer find it.{downloads} Your local copy is not affected."))
        {
            return;
        }

        var kind = Kind;
        await RunGuardedAsync($"Deleting '{deck.Title}'", async () =>
        {
            using var client = await SupabaseConnection.CreateAsync();
            var (success, message) = await client.DeleteCloudDeckAsync(deck.StoragePath, kind);

            return await AfterCloudChangeAsync(success, message);
        }, "Delete failed");
    }

    /// <summary> What an upload sends for a set or note, or null after reporting why it can't go. </summary>
    internal CloudUpload? PrepareUpload(LibraryItem item)
    {
        List<string> folderPath = IncludeFolderInfo ? DeckTransferManager.GetFolderPathNames(item.ContainerFolderID) : [];
        var description = string.Join(FolderTree.PathSeparator, folderPath);

        if (item is Note note)
        {
            var content = NoteRepository.GetContent(note.ID);
            if (string.IsNullOrWhiteSpace(content))
            {
                ReportFailure("Cannot upload an empty note.");
                return null;
            }
            if (ImageToken.Shows(content))
            {
                ReportFailure(ImagesCantUploadText(note.Name, "notes"));
                return null;
            }
            return new CloudUpload(DeckTransferManager.GenerateCloudNoteJson(note.Name, content, folderPath), description, NoteText.WordCount(content));
        }

        var cards = FlashCardRepository.GetCardsForDeck(item.ID);
        if (cards.Count == 0)
        {
            ReportFailure("Cannot upload an empty set.");
            return null;
        }
        if (DeckTransferManager.HasImages(cards))
        {
            ReportFailure(ImagesCantUploadText(item.Name, "sets"));
            return null;
        }
        return new CloudUpload(DeckTransferManager.GenerateCloudExportJson(item.ID, folderPath), description, cards.Count);
    }

    private void RefreshCloudDecks()
    {
        FilteredCloudDecks.Clear();

        var fDecks = CloudDecks.SearchAndSort(CloudSearchText, SelectedCloudSortOption?.Mode ?? SortMode.Relevance);
        foreach (var d in fDecks) FilteredCloudDecks.Add(d);
    }

    /// <summary> Throws a server's refusal so it shows as a failure; a list that won't refresh afterwards is reported separately. </summary>
    private async Task<string> AfterCloudChangeAsync(bool success, string message)
    {
        if (!success) throw new CloudActionException(message);
        if (!await LoadCloudDecksAsync()) ReportFailure(ListRefreshFailedText);
        return message;
    }

    private string ListRefreshFailedText => $"Couldn't refresh your cloud {ItemsNoun}. Check your connection.";

    private static string ImagesCantUploadText(string name, string kinds) =>
        $"'{name}' has images, and {kinds} with images can't be uploaded yet: the images would stay on this computer. Back it up from Settings to keep it safe.";

    private async Task RunGuardedAsync(string busyText, Func<Task<string>> work, string failPrefix)
    {
        if (IsBusy) return;

        IsBusy = true;
        try { await RunWithStatusAsync(busyText, work, failPrefix); }
        finally { IsBusy = false; }
    }

    private async Task LoadInitialCloudDecksAsync()
    {
        IsLoadingCloudDecks = true;
        bool loaded = await LoadCloudDecksAsync();
        IsLoadingCloudDecks = false;

        if (!loaded) await ReportFailureAsync($"Couldn't load your cloud {ItemsNoun}. Check your connection.");
    }

    /// <summary> False when the list couldn't be loaded; the list on screen is then left as it was. </summary>
    private async Task<bool> LoadCloudDecksAsync()
    {
        if (AuthSession.UserId is not { Length: > 0 } userId) return true;

        var kind = Kind;
        try
        {
            using var client = await SupabaseConnection.CreateAsync();
            var remoteDecks = await client.GetUserCloudDecksAsync(userId, kind);

            // Switched between sets and notes while this was loading: the other list is on screen now.
            if (kind != Kind) return true;

            CloudDecks.Clear();
            foreach (var deck in remoteDecks) CloudDecks.Add(deck);

            RefreshCloudDecks();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load cloud {kind.Table}", ex);
            return false;
        }
    }
}

/// <summary> An upload's file, its description (the folder path, or empty) and size (cards or words). </summary>
internal sealed record CloudUpload(string Json, string Description, int Size);
