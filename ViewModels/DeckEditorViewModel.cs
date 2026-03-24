using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.ViewModels;

public class MultiChoiceOptionEditor : ViewModelBase
{
    private string _optionText = "";
    private bool _isCorrect;

    public string OptionText
    {
        get => _optionText;
        set
        {
            _optionText = value;
            OnPropertyChanged(nameof(OptionText));
        }
    }

    public bool IsCorrect
    {
        get => _isCorrect;
        set
        {
            _isCorrect = value;
            OnPropertyChanged(nameof(IsCorrect));
        }
    }
}

public class DeckEditorViewModel : ViewModelBase
{
    public FlashCardDeck CurrentDeck { get; }
    public ObservableCollection<FlashCard> Cards { get; set; } = new();
    public ObservableCollection<MultiChoiceOptionEditor> MultiChoiceOptions { get; } = new();
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
        "Type to Answer",
        "Multi Choice"
    };

    private string _selectedCardType = "Flip";
    public string SelectedCardType
    {
        get => _selectedCardType;
        set
        {
            _selectedCardType = value;
            OnPropertyChanged(nameof(SelectedCardType));
            OnPropertyChanged(nameof(IsMultiChoiceCardType));
        }
    }

    public bool IsMultiChoiceCardType => SelectedCardType == "Multi Choice";

    private string _validationMessage = "";
    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            _validationMessage = value;
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public DeckEditorViewModel(FlashCardDeck deck)
    {
        CurrentDeck = deck;
        _deckName = deck.Name;

        AddOptionRow();
        AddOptionRow();

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
        ValidationMessage = "";

        if (string.IsNullOrWhiteSpace(NewFront) || string.IsNullOrWhiteSpace(NewBack))
        {
            ValidationMessage = "Front and back cannot be empty.";
            return;
        }

        var optionTuples = BuildValidatedMultiChoiceOptions();
        if (IsMultiChoiceCardType && optionTuples is null)
        {
            return;
        }

        if (_editingCard is not null)
        {
            _editingCard.UpdateContent(NewFront, NewBack);

            if (_editingCard is MultiFlashCard existingMulti && optionTuples is not null)
            {
                existingMulti.Options = optionTuples;
            }

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
        else if (SelectedCardType == "Multi Choice")
        {
            newCard = new MultiFlashCard(NewFront, NewBack, optionTuples ?? []);
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

        if (card is TypeFlashCard)
        {
            SelectedCardType = "Type to Answer";
        }
        else if (card is MultiFlashCard multiCard)
        {
            SelectedCardType = "Multi Choice";
            MultiChoiceOptions.Clear();
            foreach (var (optionText, isCorrect) in multiCard.Options)
            {
                MultiChoiceOptions.Add(new MultiChoiceOptionEditor
                {
                    OptionText = optionText,
                    IsCorrect = isCorrect
                });
            }
        }
        else
        {
            SelectedCardType = "Flip";
        }

        ValidationMessage = "";
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void ClearEditor()
    {
        _editingCard = null;
        NewFront = "";
        NewBack = "";
        ValidationMessage = "";

        if (IsMultiChoiceCardType)
        {
            MultiChoiceOptions.Clear();
            AddOptionRow();
            AddOptionRow();
        }

        OnPropertyChanged(nameof(SaveButtonText));
    }

    public void AddOptionRow()
    {
        if (MultiChoiceOptions.Count >= 6)
        {
            ValidationMessage = "You can add up to 6 options.";
            return;
        }

        MultiChoiceOptions.Add(new MultiChoiceOptionEditor());
        ValidationMessage = "";
    }

    public void RemoveOptionRow(MultiChoiceOptionEditor option)
    {
        if (MultiChoiceOptions.Count <= 2)
        {
            ValidationMessage = "Multi choice cards require at least 2 options.";
            return;
        }

        MultiChoiceOptions.Remove(option);
        ValidationMessage = "";
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

    private List<(string optionText, bool isCorrect)>? BuildValidatedMultiChoiceOptions()
    {
        if (!IsMultiChoiceCardType)
        {
            return null;
        }

        var options = MultiChoiceOptions
            .Select(o => (optionText: o.OptionText.Trim(), isCorrect: o.IsCorrect))
            .Where(o => !string.IsNullOrWhiteSpace(o.optionText))
            .ToList();

        if (options.Count < 2)
        {
            ValidationMessage = "Provide at least 2 non-empty options.";
            return null;
        }

        if (!options.Any(o => o.isCorrect))
        {
            ValidationMessage = "Mark at least one option as correct.";
            return null;
        }

        if (options.Select(o => o.optionText).Distinct().Count() != options.Count)
        {
            ValidationMessage = "Option text must be unique.";
            return null;
        }

        return options;
    }
}