using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;

namespace ReviFlash.ViewModels;

public partial class OnlineExportViewModel : ViewModelBase
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotAuthenticated))] 
    [NotifyPropertyChangedFor(nameof(WindowWidth))] [NotifyPropertyChangedFor(nameof(WindowHeight))]
    private bool _isAuthenticated;

    public double WindowWidth => IsAuthenticated ? 1100 : 450;
    public double WindowHeight => IsAuthenticated ? 720 : 550;
    public bool IsNotAuthenticated => !IsAuthenticated;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSignUpMode))]
    [NotifyPropertyChangedFor(nameof(HeaderText))] [NotifyPropertyChangedFor(nameof(ToggleModeText))]
    private bool _isLoginMode = true;

    public bool IsSignUpMode => !IsLoginMode;
    public string HeaderText => IsLoginMode ? "Welcome Back" : "Create an Account";
    public string ToggleModeText => IsLoginMode ? "Don't have an account? Sign up" : "Already have an account? Log in";
    public string WelcomeText => $"Welcome, {MetaDataManager.Data.SupabaseUsername}!";

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private List<FlashCardDeck> _availableDecks = [];
    [ObservableProperty] private FlashCardDeck? _selectedDeckToUpload;

    [ObservableProperty] private bool _isSelectingUpdateDeck;
    [ObservableProperty] private DeckMetadata? _targetCloudDeckToUpdate;
    [ObservableProperty] private FlashCardDeck? _selectedLocalDeckForUpdate;

    private string _localSearchText = string.Empty;
    public string LocalSearchText
    {
        get => _localSearchText;
        set { SetProperty(ref _localSearchText, value); RefreshLocalDecks(); }
    }

    private string _cloudSearchText = string.Empty;
    public string CloudSearchText
    {
        get => _cloudSearchText;
        set { SetProperty(ref _cloudSearchText, value); RefreshCloudDecks(); }
    }

    public ObservableCollection<FlashCardDeck> LocalDecks { get; } = [];
    public ObservableCollection<FlashCardDeck> FilteredLocalDecks { get; } = [];

    public ObservableCollection<DeckMetadata> CloudDecks { get; } = [];
    public ObservableCollection<DeckMetadata> FilteredCloudDecks { get; } = [];

    public OnlineExportViewModel()
    {
        if (!string.IsNullOrEmpty(MetaDataManager.Data.SupabaseAccessToken) &&  
            MetaDataManager.Data.SupabaseExpirationTime > DateTime.Now)
        {
            IsAuthenticated = true;
            OnPropertyChanged(nameof(WelcomeText));

            LocalDecks.Clear();
            foreach (var deck in FlashCardRepository.GetAllDecks())
            {
                LocalDecks.Add(deck);
            }
            RefreshLocalDecks();

            _ = LoadCloudDecksAsync();
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsLoginMode = !IsLoginMode;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Logout()
    {
        IsAuthenticated = false;
        AvailableDecks = [];
        SelectedDeckToUpload = null;

        MetaDataManager.Data.SetSupabase(null, null, null, DateTime.MinValue);
        MetaDataManager.SaveMetaData();

        StatusMessage = "You have been securely logged out.";
    }

    [RelayCommand]
    private void CancelUpdate()
    {
        IsSelectingUpdateDeck = false;
    }

    [RelayCommand()]
    private void SetupUpdate(DeckMetadata? deck)
    {
        if (deck == null) return;
        TargetCloudDeckToUpdate = deck;
        SelectedLocalDeckForUpdate = null;
        IsSelectingUpdateDeck = true;
    }

    [RelayCommand]
    private async Task SignUpAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "Username, email, and password are required to create an account.";
            return;
        }

        StatusMessage = "Creating account...";

        using var client = new SupabaseConnection();
        var (success, message, _) = await client.SignUpAsync(Email, Password, Username);

        StatusMessage = message;

        if (success)
        {
            Password = string.Empty;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Email and password are required.";
            return;
        }

        StatusMessage = "Logging in...";

        using var client = new SupabaseConnection();
        var (success, message, token, userId, username, expiration) = await client.SignInAsync(Email, Password);

        StatusMessage = message;

        if (success && !string.IsNullOrEmpty(token))
        {
            MetaDataManager.Data.SetSupabase(token, userId, username, expiration);
            Password = string.Empty;
            MetaDataManager.SaveMetaData();

            OnPropertyChanged(nameof(WelcomeText));

            LocalDecks.Clear();
            foreach (var deck in FlashCardRepository.GetAllDecks())
            {
                LocalDecks.Add(deck);
            }
            RefreshLocalDecks();

            await LoadCloudDecksAsync();

            IsAuthenticated = true;
            StatusMessage = "Successfully logged in. Ready to manage cloud data.";
        }
    }

    [RelayCommand]
    private async Task UploadAsync(FlashCardDeck? deck)
    {
        if (deck == null || string.IsNullOrEmpty(MetaDataManager.Data.SupabaseUserId)) return;

        var cards = FlashCardRepository.GetCardsForDeck(deck.ID);
        if (cards.Count == 0)
        {
            StatusMessage = "Cannot upload an empty deck.";
            return;
        }

        using var cts = new CancellationTokenSource();
        _ = AnimateStatusAsync($"Uploading '{deck.Name}'", cts.Token);

        try
        {
            string jsonPayload = DeckTransferManager.GenerateCloudExportJson(deck.ID);
            using var client = new SupabaseConnection();
            var (success, message) = await client.UploadCloudDeckAsync(MetaDataManager.Data.SupabaseUserId, deck.Name, cards.Count, jsonPayload);

            cts.Cancel();
            StatusMessage = message;
            if (success) await LoadCloudDecksAsync();
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"Upload failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ConfirmUpdateAsync()
    {
        if (TargetCloudDeckToUpdate?.StoragePath == null) return;

        if (SelectedLocalDeckForUpdate == null)
        {
            StatusMessage = "Please select a local deck to upload.";
            return;
        }

        var cards = FlashCardRepository.GetCardsForDeck(SelectedLocalDeckForUpdate.ID);
        if (cards.Count == 0)
        {
            StatusMessage = "Cannot update with an empty local deck.";
            return;
        }

        IsSelectingUpdateDeck = false;

        using var cts = new CancellationTokenSource();
        _ = AnimateStatusAsync($"Updating '{TargetCloudDeckToUpdate.Title}'", cts.Token);

        try
        {
            string jsonPayload = DeckTransferManager.GenerateCloudExportJson(SelectedLocalDeckForUpdate.ID);
            using var client = new SupabaseConnection();

            var (success, message) = await client.UpdateCloudDeckAsync(
                TargetCloudDeckToUpdate.StoragePath,
                SelectedLocalDeckForUpdate.Name,
                cards.Count,
                jsonPayload);

            cts.Cancel();
            StatusMessage = message;
            if (success) await LoadCloudDecksAsync();
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"Update failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(DeckMetadata? deck)
    {
        if (deck?.StoragePath == null) return;

        using var cts = new CancellationTokenSource();
        _ = AnimateStatusAsync($"Deleting '{deck.Title}'", cts.Token);

        try
        {
            using var client = new SupabaseConnection();
            var (success, message) = await client.DeleteCloudDeckAsync(deck.StoragePath);

            cts.Cancel();
            StatusMessage = message;
            if (success) await LoadCloudDecksAsync();
        }
        catch (Exception ex)
        {
            cts.Cancel();
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private void RefreshLocalDecks()
    {
        var searchText = LocalSearchText?.Trim().ToLowerInvariant() ?? "";
        FilteredLocalDecks.Clear();

        foreach (var deck in LocalDecks)
        {
            if (string.IsNullOrWhiteSpace(searchText) || deck.Name.ToLowerInvariant().Contains(searchText))
            {
                FilteredLocalDecks.Add(deck);
            }
        }
    }

    private void RefreshCloudDecks()
    {
        var searchText = CloudSearchText?.Trim().ToLowerInvariant() ?? "";
        FilteredCloudDecks.Clear();

        foreach (var deck in CloudDecks)
        {
            if (string.IsNullOrWhiteSpace(searchText) || (deck.Title?.ToLowerInvariant().Contains(searchText) ?? false))
            {
                FilteredCloudDecks.Add(deck);
            }
        }
    }

    private async Task LoadCloudDecksAsync()
    {
        if (string.IsNullOrEmpty(MetaDataManager.Data.SupabaseUserId)) return;

        CloudDecks.Clear();
        using var client = new SupabaseConnection();
        var remoteDecks = await client.GetUserCloudDecksAsync(MetaDataManager.Data.SupabaseUserId);

        foreach (var deck in remoteDecks)
        {
            CloudDecks.Add(deck);
        }

        RefreshCloudDecks();
    }

    private async Task AnimateStatusAsync(string baseMessage, CancellationToken token)
    {
        int dotCount = 1;
        while (!token.IsCancellationRequested)
        {
            StatusMessage = baseMessage + new string('.', dotCount);
            dotCount = (dotCount % 3) + 1;

            try
            {
                await Task.Delay(400, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}