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

public class MatchPairEditor : ViewModelBase
{
    private string _leftText = "";
    private string _rightText = "";

    public string LeftText
    {
        get => _leftText;
        set
        {
            _leftText = value;
            OnPropertyChanged(nameof(LeftText));
        }
    }

    public string RightText
    {
        get => _rightText;
        set
        {
            _rightText = value;
            OnPropertyChanged(nameof(RightText));
        }
    }
}

public class DeckEditorViewModel : ViewModelBase
{
    public FlashCardDeck CurrentDeck { get; }
    public ObservableCollection<FlashCard> Cards { get; set; } = new();
    public ObservableCollection<MultiChoiceOptionEditor> MultiChoiceOptions { get; } = new();
    public ObservableCollection<MatchPairEditor> MatchPairs { get; } = new();
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

    private string _newTypeAnswer = "";
    public string NewTypeAnswer
    {
        get => _newTypeAnswer;
        set
        {
            _newTypeAnswer = value;
            OnPropertyChanged(nameof(NewTypeAnswer));
        }
    }

    private bool _newTrueFalseAnswerIsTrue = true;
    public bool NewTrueFalseAnswerIsTrue
    {
        get => _newTrueFalseAnswerIsTrue;
        set
        {
            _newTrueFalseAnswerIsTrue = value;
            OnPropertyChanged(nameof(NewTrueFalseAnswerIsTrue));
        }
    }

    private string _newTrueOptionText = "True";
    public string NewTrueOptionText
    {
        get => _newTrueOptionText;
        set
        {
            _newTrueOptionText = value;
            OnPropertyChanged(nameof(NewTrueOptionText));
        }
    }

    private string _newFalseOptionText = "False";
    public string NewFalseOptionText
    {
        get => _newFalseOptionText;
        set
        {
            _newFalseOptionText = value;
            OnPropertyChanged(nameof(NewFalseOptionText));
        }
    }

    public string SaveButtonText => _editingCard is null ? "Save Card" : "Update Card";
    public List<string> AvailableCardTypes { get; } = new()
    {
        "Flip",
        "Type to Answer",
        "Multi Choice",
        "Match",
        "True/False"
    };

    private string _selectedCardType = "Flip";
    public string SelectedCardType
    {
        get => _selectedCardType;
        set
        {
            _selectedCardType = value;

            if (_selectedCardType == "Match")
            {
                if (string.IsNullOrWhiteSpace(NewFront))
                {
                    NewFront = "Match The Cards";
                }

                if (string.IsNullOrWhiteSpace(NewBack))
                {
                    NewBack = "Match The Cards";
                }
            }

            if (_selectedCardType == "True/False")
            {
                if (string.IsNullOrWhiteSpace(NewTrueOptionText))
                {
                    NewTrueOptionText = "True";
                }

                if (string.IsNullOrWhiteSpace(NewFalseOptionText))
                {
                    NewFalseOptionText = "False";
                }
            }

            OnPropertyChanged(nameof(SelectedCardType));
            OnPropertyChanged(nameof(IsTypeCardType));
            OnPropertyChanged(nameof(IsMultiChoiceCardType));
            OnPropertyChanged(nameof(IsMatchCardType));
            OnPropertyChanged(nameof(IsTrueFalseCardType));
            OnPropertyChanged(nameof(ShowFrontBackEditor));
        }
    }

    public bool IsTypeCardType => SelectedCardType == "Type to Answer";
    public bool IsMultiChoiceCardType => SelectedCardType == "Multi Choice";
    public bool IsMatchCardType => SelectedCardType == "Match";
    public bool IsTrueFalseCardType => SelectedCardType == "True/False";
    public bool ShowFrontBackEditor => true;

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
        AddMatchPairRow();
        AddMatchPairRow();

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

        if (IsTypeCardType && string.IsNullOrWhiteSpace(NewTypeAnswer))
        {
            ValidationMessage = "Type answer cannot be empty.";
            return;
        }

        if (_editingCard is TypeFlashCard && string.IsNullOrWhiteSpace(NewTypeAnswer))
        {
            ValidationMessage = "Type answer cannot be empty.";
            return;
        }

        var optionTuples = BuildValidatedMultiChoiceOptions();
        if (IsMultiChoiceCardType && optionTuples is null)
        {
            return;
        }

        var matchPairs = BuildValidatedMatchPairs();
        if (IsMatchCardType && matchPairs is null)
        {
            return;
        }

        var frontValue = NewFront;
        var backValue = NewBack;
        var typeAnswerValue = NewTypeAnswer.Trim();
        var trueOptionValue = NewTrueOptionText.Trim();
        var falseOptionValue = NewFalseOptionText.Trim();

        if (IsTrueFalseCardType && !ValidateTrueFalseOptions())
        {
            return;
        }

        if (_editingCard is not null)
        {
            _editingCard.UpdateContent(frontValue, backValue);

            if (_editingCard is TypeFlashCard existingType)
            {
                existingType.UpdateAnswer(typeAnswerValue);
            }

            if (_editingCard is MultiFlashCard existingMulti && optionTuples is not null)
            {
                existingMulti.Options = optionTuples;
            }

            if (_editingCard is MatchFlashCard existingMatch && matchPairs is not null)
            {
                existingMatch.Options = matchPairs;
            }

            if (_editingCard is TrueFalseFlashCard existingTrueFalse)
            {
                existingTrueFalse.UpdateTrueFalseSettings(NewTrueFalseAnswerIsTrue, trueOptionValue, falseOptionValue);
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
            newCard = new TypeFlashCard(frontValue, backValue, typeAnswerValue);
        }
        else if (SelectedCardType == "Multi Choice")
        {
            newCard = new MultiFlashCard(frontValue, backValue, optionTuples ?? []);
        }
        else if (SelectedCardType == "Match")
        {
            newCard = new MatchFlashCard(frontValue, backValue, matchPairs ?? []);
        }
        else if (SelectedCardType == "True/False")
        {
            newCard = new TrueFalseFlashCard(frontValue, backValue, NewTrueFalseAnswerIsTrue, trueOptionValue, falseOptionValue);
        }
        else
        {
            newCard = SelectedCardType == "Type to Answer"
                ? new TypeFlashCard(frontValue, backValue)
                : new FlipFlashCard(frontValue, backValue);
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
            NewTypeAnswer = ((TypeFlashCard)card).Answer;
        }
        else if (card is MultiFlashCard multiCard)
        {
            SelectedCardType = "Multi Choice";
            NewTypeAnswer = "";
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
        else if (card is MatchFlashCard matchCard)
        {
            SelectedCardType = "Match";
            NewTypeAnswer = "";
            MatchPairs.Clear();
            foreach (var (leftText, rightText) in matchCard.Options)
            {
                MatchPairs.Add(new MatchPairEditor
                {
                    LeftText = leftText,
                    RightText = rightText
                });
            }
        }
        else if (card is TrueFalseFlashCard trueFalseCard)
        {
            SelectedCardType = "True/False";
            NewTypeAnswer = "";
            NewTrueFalseAnswerIsTrue = trueFalseCard.CorrectAnswerIsTrue;
            NewTrueOptionText = trueFalseCard.TrueLabel;
            NewFalseOptionText = trueFalseCard.FalseLabel;
        }
        else
        {
            SelectedCardType = "Flip";
            NewTypeAnswer = "";
            NewTrueFalseAnswerIsTrue = true;
            NewTrueOptionText = "True";
            NewFalseOptionText = "False";
        }

        ValidationMessage = "";
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void ClearEditor()
    {
        _editingCard = null;
        if (IsMatchCardType)
        {
            NewFront = "Match The Cards";
            NewBack = "Match The Cards";
        }
        else
        {
            NewFront = "";
            NewBack = "";
        }
        NewTypeAnswer = "";
        NewTrueFalseAnswerIsTrue = true;
        NewTrueOptionText = "True";
        NewFalseOptionText = "False";
        ValidationMessage = "";

        if (IsMultiChoiceCardType)
        {
            MultiChoiceOptions.Clear();
            AddOptionRow();
            AddOptionRow();
        }

        if (IsMatchCardType)
        {
            MatchPairs.Clear();
            AddMatchPairRow();
            AddMatchPairRow();
        }

        OnPropertyChanged(nameof(SaveButtonText));
    }

    public void AddOptionRow()
    {
        if (MultiChoiceOptions.Count >= 8)
        {
            ValidationMessage = "You can add up to 8 options.";
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

    public void AddMatchPairRow()
    {
        if (MatchPairs.Count >= 8)
        {
            ValidationMessage = "You can add up to 8 match pairs.";
            return;
        }

        MatchPairs.Add(new MatchPairEditor());
        ValidationMessage = "";
    }

    public void RemoveMatchPairRow(MatchPairEditor pair)
    {
        if (MatchPairs.Count <= 2)
        {
            ValidationMessage = "Match cards require at least 2 pairs.";
            return;
        }

        MatchPairs.Remove(pair);
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

    private List<(string leftText, string rightText)>? BuildValidatedMatchPairs()
    {
        if (!IsMatchCardType)
        {
            return null;
        }

        var pairs = MatchPairs
            .Select(p => (leftText: p.LeftText.Trim(), rightText: p.RightText.Trim()))
            .Where(p => !string.IsNullOrWhiteSpace(p.leftText) && !string.IsNullOrWhiteSpace(p.rightText))
            .ToList();

        if (pairs.Count < 2)
        {
            ValidationMessage = "Provide at least 2 complete match pairs.";
            return null;
        }

        if (pairs.Select(p => p.leftText).Distinct().Count() != pairs.Count)
        {
            ValidationMessage = "Left side values must be unique.";
            return null;
        }

        if (pairs.Select(p => p.rightText).Distinct().Count() != pairs.Count)
        {
            ValidationMessage = "Right side values must be unique.";
            return null;
        }

        return pairs;
    }

    private bool ValidateTrueFalseOptions()
    {
        if (!IsTrueFalseCardType)
        {
            return true;
        }

        var trueText = NewTrueOptionText.Trim();
        var falseText = NewFalseOptionText.Trim();

        if (string.IsNullOrWhiteSpace(trueText) || string.IsNullOrWhiteSpace(falseText))
        {
            ValidationMessage = "True and False labels cannot be empty.";
            return false;
        }

        if (string.Equals(trueText, falseText, System.StringComparison.OrdinalIgnoreCase))
        {
            ValidationMessage = "True and False labels must be different.";
            return false;
        }

        NewTrueOptionText = trueText;
        NewFalseOptionText = falseText;
        return true;
    }
}