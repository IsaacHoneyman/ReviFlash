using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

using ReviFlash.Views.Controls;

namespace ReviFlash.Views;

/// <summary> The set a card goes to, and whether it is copied there rather than moved. </summary>
public sealed record MoveCardChoice(FlashCardDeck Deck, bool Copy);

/// <summary> Asks which set to move or copy a card into. Closes with a <see cref="MoveCardChoice"/>, or null. </summary>
public partial class MoveCardDialog : DialogView
{
    public MoveCardDialog() : this(null) { }

    public MoveCardDialog(FlashCardDeck? currentDeck)
    {
        InitializeComponent();

        var vm = new MoveCardViewModel(FlashCardRepository.GetAllDecks(), currentDeck?.ID ?? 0);
        vm.DeckChosen += deck => Close(new MoveCardChoice(deck, vm.IsCopy));
        DataContext = vm;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
