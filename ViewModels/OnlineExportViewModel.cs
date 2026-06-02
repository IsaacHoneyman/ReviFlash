using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ReviFlash.Services;

namespace ReviFlash.ViewModels
{
    public class OnlineExportViewModel : INotifyPropertyChanged
    {
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _username = string.Empty;
        private string _statusMessage = string.Empty;
        // Add these backing fields at the top
        private bool _isLoginMode = true;

        // Add these new properties
        public bool IsLoginMode
        {
            get => _isLoginMode;
            set
            {
                _isLoginMode = value;
                OnPropertyChanged();
                // Notify the UI that these dependent properties have also changed
                OnPropertyChanged(nameof(IsSignUpMode));
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(ToggleModeText));

                // Clear out any old errors when switching screens
                StatusMessage = string.Empty;
            }
        }

        // Helper property to make XAML binding easy (no converters needed)
        public bool IsSignUpMode => !IsLoginMode;

        // Dynamic text for the UI
        public string HeaderText => IsLoginMode ? "Welcome Back" : "Create an Account";
        public string ToggleModeText => IsLoginMode ? "Don't have an account? Sign up" : "Already have an account? Log in";

        // Add the command property
        public ICommand ToggleModeCommand { get; }

 
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

        public ICommand LoginCommand { get; }
        public ICommand SignUpCommand { get; }

        public OnlineExportViewModel()
        {
            LoginCommand = new RelayCommand(async () => await LoginAsync());
            SignUpCommand = new RelayCommand(async () => await SignUpAsync());

            // Wire up the toggle switch
            ToggleModeCommand = new RelayCommand(() => IsLoginMode = !IsLoginMode);
        }


        private async Task SignUpAsync()
        {
            // Make sure they filled out all three fields
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Username))
            {
                StatusMessage = "Username, email, and password are required to create an account.";
                return;
            }

            StatusMessage = "Creating account...";

            using var client = new SupabaseClient();

            // Pass the Username into the client
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
            var (success, message, token) = await client.SignInAsync(Email, Password);

            StatusMessage = message;

            if (success && !string.IsNullOrEmpty(token))
            {
                // ⚠️ CRITICAL NEXT STEP:
                // You now have the user's JWT token. You need to save this token securely 
                // (e.g., in memory or local storage) and attach it to future database requests 
                // instead of the Anon Key, so Supabase knows who is uploading the deck.

                // Navigate away from the login screen or change UI state here
                Password = string.Empty;
            }
        }

        // --- Standard MVVM Boilerplate Below ---

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // A minimal command implementation for button bindings
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}