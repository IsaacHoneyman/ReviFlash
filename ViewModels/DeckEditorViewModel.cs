using System;
using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data;
using ReviFlash.Utilities;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;

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
    private readonly AppMetaData _settings;
    public FlashCardDeck CurrentDeck { get; }
    public ObservableCollection<FlashCard> Cards { get; set; } = new();
    private bool _isCardsLoading;
    private CancellationTokenSource? _cardLoadCts;
    public ObservableCollection<MultiChoiceOptionEditor> MultiChoiceOptions { get; } = new();
    public ObservableCollection<MatchPairEditor> MatchPairs { get; } = new();
    private FlashCard? _editingCard;
    private ulong? _editingCardId;
    private bool _suppressCardTypeDefaultInitialization;

    private record EditorSnapshot(
        string CardType,
        string Front,
        string Back,
        string TypeAnswer,
        bool TrueFalseAnswerIsTrue,
        string TrueOptionText,
        string FalseOptionText,
        List<(string optionText, bool isCorrect)> MultiOptions,
        List<(string leftText, string rightText)> MatchPairs,
        string ValidationMessage
    );

    private EditorSnapshot? _lastSnapshot = null;

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

    public string SaveButtonText => _editingCardId.HasValue ? "Update Card" : "Save Card";
    public List<string> AvailableCardTypes { get; } = new()
    {
        GradingConstants.CARD_TYPE_FLIP,
        GradingConstants.CARD_TYPE_TYPE,
        GradingConstants.CARD_TYPE_MULTI_CHOICE,
        GradingConstants.CARD_TYPE_MATCH,
        GradingConstants.CARD_TYPE_TRUE_FALSE
    };

    private string _selectedCardType = "Flip";
    public string SelectedCardType
    {
        get => _selectedCardType;
        set
        {
            _selectedCardType = value;

            if (!_suppressCardTypeDefaultInitialization)
            {
                InitializeCardTypeDefaults();
            }

            OnPropertyChanged(nameof(SelectedCardType));
            OnPropertyChanged(nameof(IsTypeCardType));
            OnPropertyChanged(nameof(IsMultiChoiceCardType));
            OnPropertyChanged(nameof(IsMatchCardType));
            OnPropertyChanged(nameof(IsTrueFalseCardType));
            OnPropertyChanged(nameof(ShowFrontBackEditor));
        }
    }

    public bool IsTypeCardType => SelectedCardType == GradingConstants.CARD_TYPE_TYPE;
    public bool IsMultiChoiceCardType => SelectedCardType == GradingConstants.CARD_TYPE_MULTI_CHOICE;
    public bool IsMatchCardType => SelectedCardType == GradingConstants.CARD_TYPE_MATCH;
    public bool IsTrueFalseCardType => SelectedCardType == GradingConstants.CARD_TYPE_TRUE_FALSE;
    public bool ShowFrontBackEditor => true;
    public bool ShowAdditionalFieldLatexPreviews => _settings.ShowAdditionalFieldLatexPreviews;

    public bool IsCardsLoading
    {
        get => _isCardsLoading;
        private set
        {
            _isCardsLoading = value;
            OnPropertyChanged(nameof(IsCardsLoading));
        }
    }

    private void InitializeCardTypeDefaults()
    {
        if (SelectedCardType == GradingConstants.CARD_TYPE_MATCH)
        {
            if (string.IsNullOrWhiteSpace(NewFront))
            {
                NewFront = GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER;
            }

            if (string.IsNullOrWhiteSpace(NewBack))
            {
                NewBack = GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER;
            }

            if (MatchPairs.Count == 0)
            {
                AddMatchPairRow();
                AddMatchPairRow();
            }
        }

        if (SelectedCardType == GradingConstants.CARD_TYPE_MULTI_CHOICE && MultiChoiceOptions.Count == 0)
        {
            AddOptionRow();
            AddOptionRow();
        }

        if (SelectedCardType == GradingConstants.CARD_TYPE_TRUE_FALSE)
        {
            if (string.IsNullOrWhiteSpace(NewTrueOptionText))
            {
                NewTrueOptionText = GradingConstants.TRUE_LABEL;
            }

            if (string.IsNullOrWhiteSpace(NewFalseOptionText))
            {
                NewFalseOptionText = GradingConstants.FALSE_LABEL;
            }
        }
    }

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

    public DeckEditorViewModel(FlashCardDeck deck, AppMetaData settings)
    {
        _settings = settings;
        _settings.PropertyChanged += Settings_PropertyChanged;
        CurrentDeck = deck;
        _deckName = deck.Name;

        AddOptionRow();
        AddOptionRow();
        AddMatchPairRow();
        AddMatchPairRow();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppMetaData.ShowAdditionalFieldLatexPreviews))
        {
            OnPropertyChanged(nameof(ShowAdditionalFieldLatexPreviews));
        }
    }

    private void LoadCards()
    {
        var savedCards = FlashCardRepository.GetCardsForDeck(CurrentDeck.ID);
        Cards = new ObservableCollection<FlashCard>(savedCards);
        OnPropertyChanged(nameof(Cards));
    }

    public async Task LoadCardsIncrementallyAsync(int batchSize = 8)
    {
        if (IsCardsLoading && _cardLoadCts is not null)
        {
            return;
        }

        IsCardsLoading = true;
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _cardLoadCts, cts);
        previous?.Cancel();
        previous?.Dispose();
        var token = cts.Token;

        try
        {
            var savedCards = await Task.Run(() => FlashCardRepository.GetCardsForDeck(CurrentDeck.ID));

            await Dispatcher.UIThread.InvokeAsync(Cards.Clear, DispatcherPriority.Background);

            for (int i = 0; i < savedCards.Count; i += batchSize)
            {
                token.ThrowIfCancellationRequested();
                var batch = savedCards.Skip(i).Take(batchSize).ToList();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var card in batch)
                    {
                        Cards.Add(card);
                    }
                }, DispatcherPriority.Background);

                await Task.Delay(8, token);
            }
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info($"Cancelled card load for deck '{CurrentDeck.Name}' ({CurrentDeck.ID}).");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to load cards for deck '{CurrentDeck.Name}' ({CurrentDeck.ID})", ex);
        }
        finally
        {
            IsCardsLoading = false;
            if (ReferenceEquals(_cardLoadCts, cts))
            {
                _cardLoadCts = null;
                cts.Dispose();
            }
        }
    }

    public void PrepareForCardLoad()
    {
        IsCardsLoading = true;
        Cards.Clear();
    }

    public void CancelCardLoad()
    {
        _cardLoadCts?.Cancel();
        _cardLoadCts?.Dispose();
        _cardLoadCts = null;
        IsCardsLoading = false;
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

        var editingCard = GetEditingCard();

        if (editingCard is not null)
        {
            SaveEditedCard(
                editingCard,
                frontValue,
                backValue,
                typeAnswerValue,
                optionTuples,
                matchPairs,
                NewTrueFalseAnswerIsTrue,
                trueOptionValue,
                falseOptionValue);
            return;
        }

        FlashCard newCard = CreateNewCard(
            frontValue,
            backValue,
            typeAnswerValue,
            optionTuples,
            matchPairs,
            NewTrueFalseAnswerIsTrue,
            trueOptionValue,
            falseOptionValue);
        FlashCardRepository.SaveNewCard(newCard, CurrentDeck.ID);

        Cards.Add(newCard);
        ClearEditor();
    }

    private void SaveEditedCard(
        FlashCard editingCard,
        string frontValue,
        string backValue,
        string typeAnswerValue,
        List<(string optionText, bool isCorrect)>? optionTuples,
        List<(string leftText, string rightText)>? matchPairs,
        bool isTrueFalseAnswerTrue,
        string trueOptionValue,
        string falseOptionValue)
    {
        // If the selected card type matches the existing card's concrete type,
        // update the existing instance in-place. Otherwise, construct a new
        // instance of the target type (preserving the DB ID) and persist that.
        string targetTypeName = SelectedCardType switch
        {
            GradingConstants.CARD_TYPE_TYPE => nameof(TypeFlashCard),
            GradingConstants.CARD_TYPE_MULTI_CHOICE => nameof(MultiFlashCard),
            GradingConstants.CARD_TYPE_MATCH => nameof(MatchFlashCard),
            GradingConstants.CARD_TYPE_TRUE_FALSE => nameof(TrueFalseFlashCard),
            _ => nameof(FlipFlashCard)
        };

        if (editingCard.GetType().Name == targetTypeName)
        {
            editingCard.UpdateContent(frontValue, backValue);

            if (editingCard is TypeFlashCard existingType)
            {
                existingType.UpdateAnswer(typeAnswerValue);
            }

            if (editingCard is MultiFlashCard existingMulti && optionTuples is not null)
            {
                existingMulti.Options = optionTuples;
            }

            if (editingCard is MatchFlashCard existingMatch && matchPairs is not null)
            {
                existingMatch.Options = matchPairs;
            }

            if (editingCard is TrueFalseFlashCard existingTrueFalse)
            {
                existingTrueFalse.UpdateTrueFalseSettings(isTrueFalseAnswerTrue, trueOptionValue, falseOptionValue);
            }

            FlashCardRepository.UpdateCard(editingCard);
            ReplaceCardInCollection(editingCard, editingCard);
            ClearEditor();
            return;
        }

        FlashCard updatedCard = targetTypeName switch
        {
            nameof(TypeFlashCard) => new TypeFlashCard(frontValue, backValue, typeAnswerValue, editingCard.ID),
            nameof(MultiFlashCard) => new MultiFlashCard(frontValue, backValue, optionTuples ?? [], editingCard.ID),
            nameof(MatchFlashCard) => new MatchFlashCard(frontValue, backValue, matchPairs ?? [], editingCard.ID),
            nameof(TrueFalseFlashCard) => new TrueFalseFlashCard(frontValue, backValue, isTrueFalseAnswerTrue, trueOptionValue, falseOptionValue, editingCard.ID),
            _ => new FlipFlashCard(frontValue, backValue, editingCard.ID)
        };

        FlashCardRepository.UpdateCard(updatedCard);
        ReplaceCardInCollection(editingCard, updatedCard);
        ClearEditor();
    }

    private FlashCard CreateNewCard(
        string frontValue,
        string backValue,
        string typeAnswerValue,
        List<(string optionText, bool isCorrect)>? optionTuples,
        List<(string leftText, string rightText)>? matchPairs,
        bool isTrueFalseAnswerTrue,
        string trueOptionValue,
        string falseOptionValue)
    {
        return SelectedCardType switch
        {
            GradingConstants.CARD_TYPE_TYPE => new TypeFlashCard(frontValue, backValue, typeAnswerValue),
            GradingConstants.CARD_TYPE_MULTI_CHOICE => new MultiFlashCard(frontValue, backValue, optionTuples ?? []),
            GradingConstants.CARD_TYPE_MATCH => new MatchFlashCard(frontValue, backValue, matchPairs ?? []),
            GradingConstants.CARD_TYPE_TRUE_FALSE => new TrueFalseFlashCard(frontValue, backValue, isTrueFalseAnswerTrue, trueOptionValue, falseOptionValue),
            _ => new FlipFlashCard(frontValue, backValue)
        };
    }

    private void ReplaceCardInCollection(FlashCard oldCard, FlashCard newCard)
    {
        var index = Cards.IndexOf(oldCard);
        if (index < 0)
        {
            return;
        }

        Cards.RemoveAt(index);
        Cards.Insert(index, newCard);
    }

    public void BeginEditCard(FlashCard card)
    {
        _editingCard = card;
        _editingCardId = card.ID;
        LoadCardIntoEditor(card);
    }

    private void ClearEditor()
    {
        _editingCard = null;
        _editingCardId = null;
        ResetCommonEditorFields();
        ResetTypeSpecificEditorFields();
        ValidationMessage = "";

        OnPropertyChanged(nameof(SaveButtonText));
        
        // Snapshot the cleared/default editor state as the baseline.
        TakeEditorSnapshot();
    }

    private void ResetCommonEditorFields()
    {
        if (IsMatchCardType)
        {
            NewFront = GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER;
            NewBack = GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER;
            return;
        }

        NewFront = string.Empty;
        NewBack = string.Empty;
    }

    private void ResetTypeSpecificEditorFields()
    {
        NewTypeAnswer = string.Empty;
        NewTrueFalseAnswerIsTrue = true;
        NewTrueOptionText = GradingConstants.TRUE_LABEL;
        NewFalseOptionText = GradingConstants.FALSE_LABEL;

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
        if (_editingCardId.HasValue && _editingCardId.Value == card.ID)
        {
            ClearEditor();
        }

        FlashCardRepository.DeleteCard(card.ID);
        Cards.Remove(card);
    }

    // Return true when the editor is in the same state as the last snapshot
    // (i.e. no unsaved changes since the last load/save/clear). If there is no
    // snapshot yet, fall back to the original "blank" checks used for new cards.
    public bool EditorIsBlank()
    {
        if (_lastSnapshot is not null)
        {
            var snap = _lastSnapshot;

            // Compare simple fields
            if (snap.CardType != SelectedCardType) return false;
            if (snap.Front != NewFront) return false;
            if (snap.Back != NewBack) return false;
            if (snap.TypeAnswer != NewTypeAnswer) return false;
            if (snap.TrueFalseAnswerIsTrue != NewTrueFalseAnswerIsTrue) return false;
            if (snap.TrueOptionText != NewTrueOptionText) return false;
            if (snap.FalseOptionText != NewFalseOptionText) return false;
            if (snap.ValidationMessage != ValidationMessage) return false;

            // Compare multi options
            var editorMulti = MultiChoiceOptions
                .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
                .Select(o => (optionText: o.OptionText.Trim(), isCorrect: o.IsCorrect))
                .ToList();

            if (snap.MultiOptions.Count != editorMulti.Count) return false;
            for (int i = 0; i < snap.MultiOptions.Count; i++)
            {
                if (snap.MultiOptions[i].optionText != editorMulti[i].optionText || snap.MultiOptions[i].isCorrect != editorMulti[i].isCorrect)
                    return false;
            }

            // Compare match pairs
            var editorMatch = MatchPairs
                .Where(p => !(string.IsNullOrWhiteSpace(p.LeftText) && string.IsNullOrWhiteSpace(p.RightText)))
                .Select(p => (leftText: p.LeftText.Trim(), rightText: p.RightText.Trim()))
                .ToList();

            if (snap.MatchPairs.Count != editorMatch.Count) return false;
            for (int i = 0; i < snap.MatchPairs.Count; i++)
            {
                if (snap.MatchPairs[i].leftText != editorMatch[i].leftText || snap.MatchPairs[i].rightText != editorMatch[i].rightText)
                    return false;
            }

            return true;
        }

        // No snapshot — fall back to original blank heuristics
        var frontEmpty = string.IsNullOrWhiteSpace(NewFront) || (IsMatchCardType && NewFront == GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER);
        var backEmpty = string.IsNullOrWhiteSpace(NewBack) || (IsMatchCardType && NewBack == GradingConstants.CARD_TYPE_MATCH_PLACEHOLDER);
        var typeAnswerEmpty = string.IsNullOrWhiteSpace(NewTypeAnswer);
        var multiEmpty = MultiChoiceOptions.All(o => string.IsNullOrWhiteSpace(o.OptionText));
        var matchEmpty = MatchPairs.All(p => string.IsNullOrWhiteSpace(p.LeftText) && string.IsNullOrWhiteSpace(p.RightText));
        var trueFalseDefault = NewTrueFalseAnswerIsTrue == true && NewTrueOptionText == "True" && NewFalseOptionText == "False";

        return frontEmpty && backEmpty && typeAnswerEmpty && multiEmpty && matchEmpty && trueFalseDefault && string.IsNullOrWhiteSpace(ValidationMessage);
    }

    private void TakeEditorSnapshot()
    {
        var multi = MultiChoiceOptions
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .Select(o => (o.OptionText.Trim(), o.IsCorrect))
            .ToList();

        var match = MatchPairs
            .Where(p => !(string.IsNullOrWhiteSpace(p.LeftText) && string.IsNullOrWhiteSpace(p.RightText)))
            .Select(p => (p.LeftText.Trim(), p.RightText.Trim()))
            .ToList();

        _lastSnapshot = new EditorSnapshot(
            SelectedCardType,
            NewFront,
            NewBack,
            NewTypeAnswer,
            NewTrueFalseAnswerIsTrue,
            NewTrueOptionText,
            NewFalseOptionText,
            multi,
            match,
            ValidationMessage
        );
    }

    // Copy a card's content into the editor fields without starting an edit operation.
    public void CopyCardToEditor(FlashCard card)
    {
        // Copying prepares a new card draft, so clear any active edit target.
        _editingCard = null;
        _editingCardId = null;
        LoadCardIntoEditor(card);
    }

    private void LoadCardIntoEditor(FlashCard card)
    {
        _suppressCardTypeDefaultInitialization = true;
        NewFront = card.Front;
        NewBack = card.Back;
        NewTypeAnswer = "";
        NewTrueFalseAnswerIsTrue = true;
        NewTrueOptionText = "True";
        NewFalseOptionText = "False";
        MultiChoiceOptions.Clear();
        MatchPairs.Clear();

        switch (card)
        {
            case TypeFlashCard typeCard:
                SelectedCardType = GradingConstants.CARD_TYPE_TYPE;
                NewTypeAnswer = typeCard.Answer;
                break;
            case MultiFlashCard multiCard:
                SelectedCardType = GradingConstants.CARD_TYPE_MULTI_CHOICE;
                foreach (var (optionText, isCorrect) in multiCard.Options)
                {
                    MultiChoiceOptions.Add(new MultiChoiceOptionEditor
                    {
                        OptionText = optionText,
                        IsCorrect = isCorrect
                    });
                }
                break;
            case MatchFlashCard matchCard:
                SelectedCardType = GradingConstants.CARD_TYPE_MATCH;
                foreach (var (leftText, rightText) in matchCard.Options)
                {
                    MatchPairs.Add(new MatchPairEditor
                    {
                        LeftText = leftText,
                        RightText = rightText
                    });
                }
                break;
            case TrueFalseFlashCard trueFalseCard:
                SelectedCardType = GradingConstants.CARD_TYPE_TRUE_FALSE;
                NewTrueFalseAnswerIsTrue = trueFalseCard.CorrectAnswerIsTrue;
                NewTrueOptionText = trueFalseCard.TrueLabel;
                NewFalseOptionText = trueFalseCard.FalseLabel;
                break;
            default:
                SelectedCardType = GradingConstants.CARD_TYPE_FLIP;
                break;
        }

        _suppressCardTypeDefaultInitialization = false;

        ValidationMessage = "";
        OnPropertyChanged(nameof(SaveButtonText));

        // Snapshot editor contents after loading a card for editing/copying so
        // subsequent EditorIsBlank checks compare against this state.
        TakeEditorSnapshot();
    }

    private FlashCard? GetEditingCard()
    {
        if (!_editingCardId.HasValue)
        {
            _editingCard = null;
            return null;
        }

        if (_editingCard is not null && _editingCard.ID == _editingCardId.Value)
        {
            return _editingCard;
        }

        _editingCard = Cards.FirstOrDefault(card => card.ID == _editingCardId.Value);
        return _editingCard;
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