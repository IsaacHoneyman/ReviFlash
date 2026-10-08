using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

/// <summary> Picks one deck, browsed by folder, and deletes its stats. Opened from Settings. </summary>
public partial class DeleteDeckStatsViewModel : ViewModelBase
{
    public DeckFolderBrowser DeckBrowser { get; }

    [ObservableProperty] private FlashCardDeck? _selectedDeck;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public DeleteDeckStatsViewModel()
    {
        DeckBrowser = new DeckFolderBrowser("Select", deck => SelectedDeck = deck) { ShowSortOptions = false };
        DeckBrowser.SetDecks(FlashCardRepository.GetAllDecks());
    }

    public void DeleteStatsForSelectedDeck()
    {
        if (SelectedDeck is null) return;

        FlashCardRepository.DeleteStatsForDeck(SelectedDeck.ID);
        StatusMessage = $"Stats for \"{SelectedDeck.Name}\" were deleted.";
        SelectedDeck = null;
    }
}
