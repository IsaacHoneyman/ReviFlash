using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

/// <summary> Asks which set to move a card into. Closes with the chosen deck, or null. </summary>
public partial class MoveCardWindow : Window
{
    public MoveCardWindow() : this(null) { }

    public MoveCardWindow(FlashCardDeck? currentDeck)
    {
        InitializeComponent();
        Shortcuts.CloseOnEscape(this);

        var browser = new DeckFolderBrowser("Move", deck => Close(deck)) { ShowSortOptions = false };
        browser.SetDecks(FlashCardRepository.GetAllDecks().FindAll(deck => deck.ID != currentDeck?.ID));
        DataContext = browser;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
