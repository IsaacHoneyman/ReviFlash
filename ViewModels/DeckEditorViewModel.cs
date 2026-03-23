using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Collections.Generic;

namespace ReviFlash.ViewModels;

public class DeckEditorViewModel : ViewModelBase
{
    public FlashCardDeck CurrentDeck { get; }
    public ObservableCollection<FlashCard> Cards { get; set; } = new();
    private FlashCard? _editingCard;

    private string _deckName;
    public string DeckName
    {
        get => _deckName;
        set
        {
            _deckName = value;
            CurrentDeck.Name = value;
            FlashCardRepository.UpdateDeck(CurrentDeck);
        }
    }

    private string _newFront = "";
    public string NewFront
    {
        get => _newFront;
        set
        {
            _newFront = value;
            OnPropertyChanged(nameof(NewFront));
        }
    }
    private string _newBack = "";
    public string NewBack
    {
        get => _newBack;
        set
        {
            _newBack = value;
            OnPropertyChanged(nameof(NewBack));
        }
    }

    public string SaveButtonText => _editingCard is null ? "Save Card" : "Update Card";
    public List<string> AvailableCardTypes { get; } = new()
    {
        "Flip",
        "Type to Answer"
    };

    public string SelectedCardType { get; set; } = "Flip";

    public DeckEditorViewModel(FlashCardDeck deck)
    {
        CurrentDeck = deck;
        _deckName = deck.Name;

        LoadCards();
    }

    private void LoadCards()
    {
        Cards.Clear();
        var savedCards = FlashCardRepository.GetCardsForDeck(CurrentDeck.ID);
        foreach (var card in savedCards)
        {
            Cards.Add(card);
        }
    }

    public void AddNewCard()
    {
        if (string.IsNullOrWhiteSpace(NewFront) || string.IsNullOrWhiteSpace(NewBack))
            return;

        if (_editingCard is not null)
        {
            _editingCard.UpdateContent(NewFront, NewBack);
            FlashCardRepository.UpdateCard(_editingCard);

            // Force item refresh in the bound collection.
            var index = Cards.IndexOf(_editingCard);
            if (index >= 0)
            {
                Cards.RemoveAt(index);
                Cards.Insert(index, _editingCard);
            }

            ClearEditor();
            return;
        }

        FlashCard newCard;

        if (SelectedCardType == "Type to Answer")
        {
            newCard = new TypeFlashCard(NewFront, NewBack);
        }
        else
        {
            newCard = new FlipFlashCard(NewFront, NewBack);
        }

        FlashCardRepository.SaveNewCard(newCard, CurrentDeck.ID);

        Cards.Add(newCard);
        ClearEditor();
    }

    public void BeginEditCard(FlashCard card)
    {
        _editingCard = card;
        NewFront = card.Front;
        NewBack = card.Back;
        SelectedCardType = card is TypeFlashCard ? "Type to Answer" : "Flip";
        OnPropertyChanged(nameof(SelectedCardType));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void ClearEditor()
    {
        _editingCard = null;
        NewFront = "";
        NewBack = "";
        OnPropertyChanged(nameof(SaveButtonText));
    }

    public void DeleteCard(FlashCard card)
    {
        if (_editingCard == card)
        {
            ClearEditor();
        }

        FlashCardRepository.DeleteCard(card.ID);
        Cards.Remove(card);
    }
}