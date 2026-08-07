using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ReviFlash.Services;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Threading;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReviFlash.ViewModels
{
    public class OnlineExportViewModel : INotifyPropertyChanged
    {
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

        public double WindowWidth => IsAuthenticated ? 1100 : 450;
        public double WindowHeight => IsAuthenticated ? 720 : 550;

        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _username = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoginMode = true;
        private bool _isAuthenticated;

        private List<FlashCardDeck> _availableDecks = new();
        private FlashCardDeck? _selectedDeckToUpload;

        public List<FlashCardDeck> AvailableDecks
        {
            get => _availableDecks;
            set { _availableDecks = value; OnPropertyChanged(); }
        }

        public FlashCardDeck? SelectedDeckToUpload
        {
            get => _selectedDeckToUpload;
            set { _selectedDeckToUpload = value; OnPropertyChanged(); }
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                _isAuthenticated = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotAuthenticated));
                OnPropertyChanged(nameof(WindowWidth));
                OnPropertyChanged(nameof(WindowHeight));
            }
        }

        public bool IsNotAuthenticated => !IsAuthenticated;

        public bool IsLoginMode
        {
            get => _isLoginMode;
            set
            {
                _isLoginMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSignUpMode));
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(ToggleModeText));

                StatusMessage = string.Empty;
            }
        }

        public bool IsSignUpMode => !IsLoginMode;

        public string HeaderText => IsLoginMode ? "Welcome Back" : "Create an Account";
        public string ToggleModeText => IsLoginMode ? "Don't have an account? Sign up" : "Already have an account? Log in";
        public string WelcomeText => $"Welcome, {SupabaseConfig.CurrentUsername}!";

        public ICommand ToggleModeCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand SignUpCommand { get; }

        public ICommand UploadCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public ObservableCollection<FlashCardDeck> LocalDecks { get; } = [];
        public ObservableCollection<FlashCardDeck> FilteredLocalDecks { get; } = [];

        public ObservableCollection<DeckMetadata> CloudDecks { get; } = [];
        public ObservableCollection<DeckMetadata> FilteredCloudDecks { get; } = [];

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private string _localSearchText = string.Empty;
        public string LocalSearchText
        {
            get => _localSearchText;
            set { _localSearchText = value; OnPropertyChanged(); RefreshLocalDecks(); }
        }

        private string _cloudSearchText = string.Empty;
        public string CloudSearchText
        {
            get => _cloudSearchText;
            set { _cloudSearchText = value; OnPropertyChanged(); RefreshCloudDecks(); }
        }

        private bool _isSelectingUpdateDeck;
        public bool IsSelectingUpdateDeck
        {
            get => _isSelectingUpdateDeck;
            set { _isSelectingUpdateDeck = value; OnPropertyChanged(); }
        }

        private DeckMetadata? _targetCloudDeckToUpdate;
        public DeckMetadata? TargetCloudDeckToUpdate
        {
            get => _targetCloudDeckToUpdate;
            set { _targetCloudDeckToUpdate = value; OnPropertyChanged(); }
        }

        private FlashCardDeck? _selectedLocalDeckForUpdate;
        public FlashCardDeck? SelectedLocalDeckForUpdate
        {
            get => _selectedLocalDeckForUpdate;
            set { _selectedLocalDeckForUpdate = value; OnPropertyChanged(); }
        }

        public ICommand ConfirmUpdateCommand { get; }
        public ICommand CancelUpdateCommand { get; }

        public OnlineExportViewModel()
        {
            ConfirmUpdateCommand = new RelayCommand(async () => await ConfirmUpdateAsync());
            CancelUpdateCommand = new RelayCommand(() => IsSelectingUpdateDeck = false);

            UpdateCommand = new RelayCommand<DeckMetadata>(deck =>
            {
                if (deck == null) return;
                TargetCloudDeckToUpdate = deck;
                SelectedLocalDeckForUpdate = null;
                IsSelectingUpdateDeck = true;
            });

            LoginCommand = new RelayCommand(async () => await LoginAsync());
            SignUpCommand = new RelayCommand(async () => await SignUpAsync());
            ToggleModeCommand = new RelayCommand(() => IsLoginMode = !IsLoginMode);
            LogoutCommand = new RelayCommand(Logout);

            UploadCommand = new RelayCommand<FlashCardDeck>(async (d) => await UploadAsync(d));
            DeleteCommand = new RelayCommand<DeckMetadata>(async (d) => await DeleteAsync(d));

            var metaData = MetaDataManager.LoadMetaDataOnStartup();

            if (!string.IsNullOrEmpty(metaData.SupabaseAccessToken))
            {
                SupabaseConfig.CurrentAccessToken = metaData.SupabaseAccessToken;
                SupabaseConfig.CurrentUserId = metaData.SupabaseUserId;
                SupabaseConfig.CurrentUsername = metaData.SupabaseUsername;

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
            if (string.IsNullOrEmpty(SupabaseConfig.CurrentUserId)) return;

            CloudDecks.Clear();
            using var client = new SupabaseClient();
            var remoteDecks = await client.GetUserDecksAsync(SupabaseConfig.CurrentUserId);

            foreach (var deck in remoteDecks)
            {
                CloudDecks.Add(deck);
            }

            RefreshCloudDecks();
        }

        private async Task SignUpAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Username))
            {
                StatusMessage = "Username, email, and password are required to create an account.";
                return;
            }

            StatusMessage = "Creating account...";

            using var client = new SupabaseClient();
            var (success, message, _) = await client.SignUpAsync(Email, Password, Username);

            StatusMessage = message;

            if (success)
            {
                Password = string.Empty;
            }
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Email and password are required.";
                return;
            }

            StatusMessage = "Logging in...";

            using var client = new SupabaseClient();
            var (success, message, token, userId, username) = await client.SignInAsync(Email, Password);

            StatusMessage = message;

            if (success && !string.IsNullOrEmpty(token))
            {
                SupabaseConfig.CurrentAccessToken = token;
                SupabaseConfig.CurrentUserId = userId;
                SupabaseConfig.CurrentUsername = username;
                Password = string.Empty;

                var metaData = MetaDataManager.LoadMetaDataOnStartup();
                metaData.SupabaseAccessToken = token;
                metaData.SupabaseUserId = userId;
                metaData.SupabaseUsername = username;
                MetaDataManager.SaveMetaData(metaData);

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

        private void Logout()
        {
            SupabaseConfig.CurrentAccessToken = null;
            SupabaseConfig.CurrentUserId = null;
            SupabaseConfig.CurrentUsername = null;
            IsAuthenticated = false;

            AvailableDecks = [];
            SelectedDeckToUpload = null;

            var metaData = MetaDataManager.LoadMetaDataOnStartup();
            metaData.SupabaseAccessToken = null;
            metaData.SupabaseUserId = null;
            metaData.SupabaseUsername = null;
            MetaDataManager.SaveMetaData(metaData);

            StatusMessage = "You have been securely logged out.";
        }

        private async Task UploadAsync(FlashCardDeck? deck)
        {
            if (deck == null || string.IsNullOrEmpty(SupabaseConfig.CurrentUserId)) return;

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
                string jsonPayload = BackupManager.GenerateCloudExportJson(deck.ID);
                using var client = new SupabaseClient();
                var (success, message) = await client.UploadDeckAsync(SupabaseConfig.CurrentUserId, deck.Name, cards.Count, jsonPayload);

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
                string jsonPayload = BackupManager.GenerateCloudExportJson(SelectedLocalDeckForUpdate.ID);
                using var client = new SupabaseClient();

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

        private async Task DeleteAsync(DeckMetadata? deck)
        {
            if (deck?.StoragePath == null) return;

            using var cts = new CancellationTokenSource();
            _ = AnimateStatusAsync($"Deleting '{deck.Title}'", cts.Token);

            try
            {
                using var client = new SupabaseClient();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand(Action execute) : ICommand
    {
        private readonly Action _execute = execute;

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}