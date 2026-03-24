using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Data;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void DeleteAllStats_Click(object? sender, RoutedEventArgs e)
    {
        var confirmDialog = new ConfirmDialogWindow(
            "Are you sure you want to delete all flashcard stats across all decks?"
        );

        bool confirmed = await confirmDialog.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        FlashCardRepository.DeleteAllStats();

        if (Owner is MainWindow { DataContext: MainWindowViewModel mainVm })
        {
            mainVm.RefreshStats();
        }

        var statusText = this.FindControl<TextBlock>("DeleteStatsStatusText");
        if (statusText != null)
        {
            statusText.Text = "All flashcard stats were deleted.";
            statusText.IsVisible = true;
        }
    }

    private async void DeleteDeckStats_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { SelectedDeckForStatDeletion: not null } vm)
        {
            var deckName = vm.SelectedDeckForStatDeletion.Name;
            var confirmDialog = new ConfirmDialogWindow(
                $"Are you sure you want to delete all stats for \"{deckName}\"? This cannot be undone."
            );

            bool confirmed = await confirmDialog.ShowDialog<bool>(this);
            if (!confirmed)
            {
                return;
            }

            vm.DeleteStatsForSelectedDeck();

            if (Owner is MainWindow { DataContext: MainWindowViewModel mainVm })
            {
                mainVm.RefreshStats();
            }

            var statusText = this.FindControl<TextBlock>("DeleteStatsStatusText");
            if (statusText != null)
            {
                statusText.Text = $"Stats for \"{deckName}\" were deleted.";
                statusText.IsVisible = true;
            }
        }
    }
}