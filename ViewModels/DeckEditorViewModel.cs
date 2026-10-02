using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;

using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.ViewModels;

public partial class MultiChoiceOptionEditor : ViewModelBase
{
    [ObservableProperty] private string _optionText = "";
    [ObservableProperty] private bool _isCorrect;
}

public partial class MatchPairEditor : ViewModelBase
{
    [ObservableProperty] private string _leftText = "";
    [ObservableProperty] private string _rightText = "";
}

public partial class DeckEditorViewModel : ViewModelBase, IDisposable
{
    private const int MinRows = 2;
    private const int MaxRows = 8;

    private bool _disposed;
    private CancellationTokenSource? _cardLoadCts;

    /// <summary> The saved card being edited, or null when the editor makes a new card. </summary>
    private FlashCard? _editingCard;
    private bool _suppressCardTypeDefaults;

    /// <summary> What the editor held when it was last cleared or loaded, to spot unsaved changes. </summary>
    private EditorState _savedState;

    public FlashCardDeck CurrentDeck { get; }
    [ObservableProperty] private ObservableCollection<FlashCard> _cards = new();
    public ObservableCollection<MultiChoiceOptionEditor> MultiChoiceOptions { get; } = new();
    public ObservableCollection<MatchPairEditor> MatchPairs { get; } = new();

    [ObservableProperty] private bool _isCardsLoading;

    [ObservableProperty] private string _deckName;
    partial void OnDeckNameChanged(string value)
    {
        CurrentDeck.Name = value;
        FlashCardRepository.UpdateDeck(CurrentDeck);
    }

    // --- Editor fields ---

    [ObservableProperty] private string _newFront = "";
    [ObservableProperty] private string _newBack = "";
    [ObservableProperty] private string _newTypeAnswer = "";
    [ObservableProperty] private bool _newIsReversible;
    [ObservableProperty] private bool _newTrueFalseAnswerIsTrue = true;
    [ObservableProperty] private string _newTrueOptionText = TRUE_LABEL;
    [ObservableProperty] private string _newFalseOptionText = FALSE_LABEL;

    public List<string> AvailableCardTypes { get; } =
    [
        CARD_TYPE_FLIP,
        CARD_TYPE_TYPE,
        CARD_TYPE_MULTI_CHOICE,
        CARD_TYPE_MATCH,
        CARD_TYPE_TRUE_FALSE
    ];

    [NotifyPropertyChangedFor(nameof(IsFlipCardType))]
    [NotifyPropertyChangedFor(nameof(IsTypeCardType))]
    [NotifyPropertyChangedFor(nameof(IsMultiChoiceCardType))]
    [NotifyPropertyChangedFor(nameof(IsMatchCardType))]
    [NotifyPropertyChangedFor(nameof(IsTrueFalseCardType))]
    [ObservableProperty] private string _selectedCardType = CARD_TYPE_FLIP;
    partial void OnSelectedCardTypeChanged(string value)
    {
        if (!_suppressCardTypeDefaults) ApplyCardTypeDefaults();
    }

    public bool IsFlipCardType => SelectedCardType == CARD_TYPE_FLIP;
    public bool IsTypeCardType => SelectedCardType == CARD_TYPE_TYPE;
    public bool IsMultiChoiceCardType => SelectedCardType == CARD_TYPE_MULTI_CHOICE;
    public bool IsMatchCardType => SelectedCardType == CARD_TYPE_MATCH;
    public bool IsTrueFalseCardType => SelectedCardType == CARD_TYPE_TRUE_FALSE;

    public bool ShowAdditionalFieldLatexPreviews => MetaDataManager.Data.ShowAdditionalFieldLatexPreviews;
    public string SaveButtonText => _editingCard is null ? "Save Card" : "Update Card";

    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    [ObservableProperty] private string _validationMessage = "";
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public DeckEditorViewModel(FlashCardDeck deck)
    {
        CurrentDeck = deck;
        _deckName = deck.Name;

        MetaDataManager.Data.PropertyChanged += Settings_PropertyChanged;

        ResetRows(MultiChoiceOptions);
        ResetRows(MatchPairs);
        _savedState = CaptureState();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppMetaData.ShowAdditionalFieldLatexPreviews))
            OnPropertyChanged(nameof(ShowAdditionalFieldLatexPreviews));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        MetaDataManager.Data.PropertyChanged -= Settings_PropertyChanged;
    }

    // --- Card list ---

    public async Task LoadCardsIncrementallyAsync(int batchSize = 8)
    {
        if (IsCardsLoading && _cardLoadCts is not null) return;

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

            foreach (var batch in savedCards.Chunk(batchSize))
            {
                token.ThrowIfCancellationRequested();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var card in batch) Cards.Add(card);
                }, DispatcherPriority.Background);

                await Task.Delay(8, token);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo($"Cancelled card load for deck '{CurrentDeck.Name}' ({CurrentDeck.ID}).");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load cards for deck '{CurrentDeck.Name}' ({CurrentDeck.ID})", ex);
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

    public void DeleteCard(FlashCard card)
    {
        if (_editingCard?.ID == card.ID) ClearEditor();

        FlashCardRepository.DeleteCard(card.ID);
        Cards.Remove(card);
    }

    // --- Saving ---

    /// <summary> Saves the editor as a new card, or over the card being edited (which may change its type). </summary>
    public void AddNewCard()
    {
        ValidationMessage = Validate() ?? "";
        if (HasValidationMessage) return;

        if (IsTrueFalseCardType)
        {
            NewTrueOptionText = NewTrueOptionText.Trim();
            NewFalseOptionText = NewFalseOptionText.Trim();
        }

        if (_editingCard is { } editingCard)
        {
            var updatedCard = BuildCard(editingCard.ID);
            FlashCardRepository.UpdateCard(updatedCard);

            var index = Cards.IndexOf(editingCard);
            if (index >= 0) Cards[index] = updatedCard;
        }
        else
        {
            var newCard = BuildCard(id: null);
            FlashCardRepository.SaveNewCard(newCard, CurrentDeck.ID);
            Cards.Add(newCard);
        }

        ClearEditor();
    }

    /// <summary> The first problem with the editor's contents, or null when it can be saved. </summary>
    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(NewFront) || string.IsNullOrWhiteSpace(NewBack))
            return "Front and back cannot be empty.";

        if (IsTypeCardType && string.IsNullOrWhiteSpace(NewTypeAnswer))
            return "Type answer cannot be empty.";

        if (IsMultiChoiceCardType)
        {
            var options = FilledOptions();
            if (options.Count < MinRows) return "Provide at least 2 non-empty options.";
            if (!options.Any(o => o.isCorrect)) return "Mark at least one option as correct.";
            if (options.Select(o => o.optionText).Distinct().Count() != options.Count) return "Option text must be unique.";
        }

        if (IsMatchCardType)
        {
            var pairs = CompletePairs();
            if (pairs.Count < MinRows) return "Provide at least 2 complete match pairs.";
            if (pairs.Select(p => p.leftText).Distinct().Count() != pairs.Count) return "Left side values must be unique.";
            if (pairs.Select(p => p.rightText).Distinct().Count() != pairs.Count) return "Right side values must be unique.";
        }

        if (IsTrueFalseCardType)
        {
            var trueText = NewTrueOptionText.Trim();
            var falseText = NewFalseOptionText.Trim();
            if (trueText.Length == 0 || falseText.Length == 0) return "True and False labels cannot be empty.";
            if (string.Equals(trueText, falseText, StringComparison.OrdinalIgnoreCase)) return "True and False labels must be different.";
        }

        return null;
    }

    /// <summary> A card of the selected type from the editor; <paramref name="id"/> is the saved card's when editing. </summary>
    private FlashCard BuildCard(ulong? id)
    {
        var front = NewFront;
        var back = NewBack;
        var cardId = id ?? ulong.MaxValue;

        FlashCard card = SelectedCardType switch
        {
            CARD_TYPE_TYPE => new TypeFlashCard(front, back, NewTypeAnswer.Trim(), cardId),
            CARD_TYPE_MULTI_CHOICE => new MultiFlashCard(front, back, FilledOptions(), cardId),
            CARD_TYPE_MATCH => new MatchFlashCard(front, back, CompletePairs(), cardId),
            CARD_TYPE_TRUE_FALSE => new TrueFalseFlashCard(front, back, NewTrueFalseAnswerIsTrue, NewTrueOptionText, NewFalseOptionText, cardId),
            _ => new FlipFlashCard(front, back, cardId, NewIsReversible),
        };
        return card;
    }

    private List<(string optionText, bool isCorrect)> FilledOptions() => MultiChoiceOptions
        .Select(o => (optionText: o.OptionText.Trim(), isCorrect: o.IsCorrect))
        .Where(o => o.optionText.Length > 0)
        .ToList();

    private List<(string leftText, string rightText)> CompletePairs() => MatchPairs
        .Select(p => (leftText: p.LeftText.Trim(), rightText: p.RightText.Trim()))
        .Where(p => p.leftText.Length > 0 && p.rightText.Length > 0)
        .ToList();

    // --- Loading cards into the editor ---

    public void BeginEditCard(FlashCard card)
    {
        _editingCard = card;
        LoadCardIntoEditor(card);
    }

    public void CopyCardToEditor(FlashCard card)
    {
        _editingCard = null;
        LoadCardIntoEditor(card);
    }

    private void LoadCardIntoEditor(FlashCard card)
    {
        _suppressCardTypeDefaults = true;

        SelectedCardType = card switch
        {
            TypeFlashCard => CARD_TYPE_TYPE,
            MultiFlashCard => CARD_TYPE_MULTI_CHOICE,
            MatchFlashCard => CARD_TYPE_MATCH,
            TrueFalseFlashCard => CARD_TYPE_TRUE_FALSE,
            _ => CARD_TYPE_FLIP,
        };
        NewFront = card.Front;
        NewBack = card.Back;
        NewTypeAnswer = (card as TypeFlashCard)?.Answer ?? "";
        NewIsReversible = card is FlipFlashCard { IsReversible: true };

        var trueFalse = card as TrueFalseFlashCard;
        NewTrueFalseAnswerIsTrue = trueFalse?.CorrectAnswerIsTrue ?? true;
        NewTrueOptionText = trueFalse?.TrueLabel ?? TRUE_LABEL;
        NewFalseOptionText = trueFalse?.FalseLabel ?? FALSE_LABEL;

        MultiChoiceOptions.Clear();
        foreach (var (optionText, isCorrect) in (card as MultiFlashCard)?.Options ?? [])
            MultiChoiceOptions.Add(new MultiChoiceOptionEditor { OptionText = optionText, IsCorrect = isCorrect });

        MatchPairs.Clear();
        foreach (var (leftText, rightText) in (card as MatchFlashCard)?.Options ?? [])
            MatchPairs.Add(new MatchPairEditor { LeftText = leftText, RightText = rightText });

        _suppressCardTypeDefaults = false;
        FinishEditorReset();
    }

    private void ClearEditor()
    {
        _editingCard = null;

        var isMatch = IsMatchCardType;
        NewFront = isMatch ? CARD_TYPE_MATCH_PLACEHOLDER : "";
        NewBack = isMatch ? CARD_TYPE_MATCH_PLACEHOLDER : "";
        NewTypeAnswer = "";
        NewIsReversible = false;
        NewTrueFalseAnswerIsTrue = true;
        NewTrueOptionText = TRUE_LABEL;
        NewFalseOptionText = FALSE_LABEL;

        if (IsMultiChoiceCardType) ResetRows(MultiChoiceOptions);
        if (isMatch) ResetRows(MatchPairs);

        FinishEditorReset();
    }

    private void FinishEditorReset()
    {
        ValidationMessage = "";
        OnPropertyChanged(nameof(SaveButtonText));
        _savedState = CaptureState();
    }

    /// <summary> Fills in what a newly picked card type needs, without touching anything already typed. </summary>
    private void ApplyCardTypeDefaults()
    {
        if (IsMatchCardType)
        {
            if (string.IsNullOrWhiteSpace(NewFront)) NewFront = CARD_TYPE_MATCH_PLACEHOLDER;
            if (string.IsNullOrWhiteSpace(NewBack)) NewBack = CARD_TYPE_MATCH_PLACEHOLDER;
            if (MatchPairs.Count == 0) ResetRows(MatchPairs);
        }

        if (IsMultiChoiceCardType && MultiChoiceOptions.Count == 0) ResetRows(MultiChoiceOptions);

        if (IsTrueFalseCardType)
        {
            if (string.IsNullOrWhiteSpace(NewTrueOptionText)) NewTrueOptionText = TRUE_LABEL;
            if (string.IsNullOrWhiteSpace(NewFalseOptionText)) NewFalseOptionText = FALSE_LABEL;
        }
    }

    // --- Option and pair rows ---

    public void AddOptionRow() => AddRow(MultiChoiceOptions, "You can add up to 8 options.");
    public void RemoveOptionRow(MultiChoiceOptionEditor option) => RemoveRow(MultiChoiceOptions, option, "Multi choice cards require at least 2 options.");
    public void AddMatchPairRow() => AddRow(MatchPairs, "You can add up to 8 match pairs.");
    public void RemoveMatchPairRow(MatchPairEditor pair) => RemoveRow(MatchPairs, pair, "Match cards require at least 2 pairs.");

    private void AddRow<T>(ObservableCollection<T> rows, string limitMessage) where T : new()
    {
        if (rows.Count >= MaxRows)
        {
            ValidationMessage = limitMessage;
            return;
        }

        rows.Add(new T());
        ValidationMessage = "";
    }

    private void RemoveRow<T>(ObservableCollection<T> rows, T row, string limitMessage)
    {
        if (rows.Count <= MinRows)
        {
            ValidationMessage = limitMessage;
            return;
        }

        rows.Remove(row);
        ValidationMessage = "";
    }

    private static void ResetRows<T>(ObservableCollection<T> rows) where T : new()
    {
        rows.Clear();
        for (var i = 0; i < MinRows; i++) rows.Add(new T());
    }

    // --- Unsaved changes ---

    /// <summary> True when nothing has changed since the editor was last cleared or loaded. </summary>
    public bool EditorIsBlank() => CaptureState() == _savedState;

    /// <summary> The editor's contents in a form records can compare (blank option and pair rows ignored). </summary>
    private sealed record EditorState(
        string CardType, string Front, string Back, string TypeAnswer, bool IsReversible,
        bool TrueFalseAnswerIsTrue, string TrueOptionText, string FalseOptionText,
        string Options, string Pairs);

    private EditorState CaptureState()
    {
        var options = MultiChoiceOptions
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .Select(o => $"{o.OptionText.Trim()}\u001F{o.IsCorrect}");

        var pairs = MatchPairs
            .Where(p => !string.IsNullOrWhiteSpace(p.LeftText) || !string.IsNullOrWhiteSpace(p.RightText))
            .Select(p => $"{p.LeftText.Trim()}\u001F{p.RightText.Trim()}");

        return new EditorState(
            SelectedCardType, NewFront, NewBack, NewTypeAnswer, NewIsReversible,
            NewTrueFalseAnswerIsTrue, NewTrueOptionText, NewFalseOptionText,
            string.Join('\u001E', options), string.Join('\u001E', pairs));
    }
}
