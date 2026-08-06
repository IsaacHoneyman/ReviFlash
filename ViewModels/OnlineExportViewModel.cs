using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ReviFlash.Services;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Text.Json;
using System.Threading;

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

        public OnlineExportViewModel()
        {
            LoginCommand = new RelayCommand(async () => await LoginAsync());
            SignUpCommand = new RelayCommand(async () => await SignUpAsync());
            ToggleModeCommand = new RelayCommand(() => IsLoginMode = !IsLoginMode);

            UploadCommand = new RelayCommand(async () => await UploadAsync());
            LogoutCommand = new RelayCommand(Logout);

            if (!string.IsNullOrEmpty(SupabaseConfig.CurrentAccessToken))
            {
                IsAuthenticated = true;
                AvailableDecks = FlashCardRepository.GetAllDecks();
                StatusMessage = "Session restored. Ready to upload.";
            }
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

                OnPropertyChanged(nameof(WelcomeText));
                AvailableDecks = FlashCardRepository.GetAllDecks();

                IsAuthenticated = true;
                StatusMessage = "Successfully logged in. Ready to upload.";
            }
        }

        private void Logout()
        {
            SupabaseConfig.CurrentAccessToken = null;
            IsAuthenticated = false;

            AvailableDecks = [];
            SelectedDeckToUpload = null;

            StatusMessage = "You have been securely logged out.";
        }

        private async Task UploadAsync()
        {
            if (SelectedDeckToUpload == null)
            {
                StatusMessage = "Please select a flashcard set to upload.";
                return;
            }

            if (string.IsNullOrEmpty(SupabaseConfig.CurrentUserId))
            {
                StatusMessage = "Authentication error: User ID missing.";
                return;
            }

            var cards = FlashCardRepository.GetCardsForDeck(SelectedDeckToUpload.ID);

            if (cards.Count == 0)
            {
                StatusMessage = "Cannot upload an empty deck.";
                return;
            }

            using var cts = new CancellationTokenSource();
            var animationTask = AnimateStatusAsync($"Uploading '{SelectedDeckToUpload.Name}'", cts.Token);

            try
            {
                string jsonPayload = BackupManager.GenerateCloudExportJson(SelectedDeckToUpload.ID);

                using var client = new SupabaseClient();
                var (success, message) = await client.UploadDeckAsync(
                    SupabaseConfig.CurrentUserId,
                    SelectedDeckToUpload.Name,
                    cards.Count,
                    jsonPayload
                );

                cts.Cancel();
                StatusMessage = message; 
            }
            catch (Exception ex)
            {
                cts.Cancel();
                StatusMessage = $"Upload failed: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}