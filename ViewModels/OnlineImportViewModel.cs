using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels
{
    public class OnlineImportViewModel : INotifyPropertyChanged
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

        private string _searchText = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isSearching;

        public ObservableCollection<DeckMetadata> SearchResults { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand { get; }
        public ICommand DownloadCommand { get; }

        public OnlineImportViewModel()
        {
            SearchCommand = new RelayCommand(async () => await PerformSearchAsync());
            DownloadCommand = new RelayCommand<DeckMetadata>(async (deck) => await DownloadDeckAsync(deck));

            _ = PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            IsSearching = true;
            SearchResults.Clear();

            using var cts = new CancellationTokenSource();
            var animationTask = AnimateStatusAsync("Searching public flashcards", cts.Token);

            try
            {
                using var client = new SupabaseClient();
                var results = await client.GetPublicDecksAsync(SearchText, limit: 25);

                foreach (var deck in results)
                {
                    SearchResults.Add(deck);
                }

                cts.Cancel();
                StatusMessage = SearchResults.Count == 0 ? "No decks found matching your search." : $"Found {SearchResults.Count} decks.";
            }
            catch (Exception ex)
            {
                cts.Cancel();
                StatusMessage = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task DownloadDeckAsync(DeckMetadata? deck)
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
                using var client = new SupabaseClient();
                string json = await client.DownloadCloudDeckJsonAsync(deck.StoragePath);
                BackupManager.TryImportCloudDeck(json);

                cts.Cancel();
                StatusMessage = $"Successfully imported '{deck.Title}'! You can now review it.";
            }
            catch (Exception ex)
            {
                cts.Cancel();
                StatusMessage = $"Import failed: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Generic RelayCommand for passing command parameters (like the selected deck)
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}