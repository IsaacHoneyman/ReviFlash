using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

/// <summary> Picks a set to move or copy a card into; moving never offers the card's own set. </summary>
public partial class MoveCardViewModel : ViewModelBase
{
    private readonly List<FlashCardDeck> _decks;
    private readonly ulong _currentDeckID;

    public DeckFolderBrowser Browser { get; }

    /// <summary> Raised with the set whose button was clicked. </summary>
    public event Action<FlashCardDeck>? DeckChosen;

    [NotifyPropertyChangedFor(nameof(IsMove), nameof(IsCopySelected), nameof(Hint))]
    [ObservableProperty] private bool _isCopy;
    partial void OnIsCopyChanged(bool value)
    {
        Browser.ActionText = value ? "Copy" : "Move";
        ShowDecks();
    }

    // Bound to toggle buttons, so clicking the selected one keeps it selected.
    public bool IsMove
    {
        get => !IsCopy;
        set { if (value) IsCopy = false; else OnPropertyChanged(); }
    }

    public bool IsCopySelected
    {
        get => IsCopy;
        set { if (value) IsCopy = true; else OnPropertyChanged(); }
    }

    public string Hint => IsCopy
        ? "Adds a copy of this card to the set you choose. The copy starts with no answer history."
        : "Moves this card into the set you choose. Its answer history goes with it.";

    public MoveCardViewModel(IEnumerable<FlashCardDeck> decks, ulong currentDeckID)
    {
        _decks = [.. decks];
        _currentDeckID = currentDeckID;
        Browser = new DeckFolderBrowser("Move", deck => DeckChosen?.Invoke(deck)) { ShowSortOptions = false };
        ShowDecks();
    }

    private void ShowDecks() => Browser.SetDecks(IsCopy ? _decks : _decks.Where(deck => deck.ID != _currentDeckID));
}
