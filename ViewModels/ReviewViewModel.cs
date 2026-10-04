using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Models;
using ReviFlash.Data.Local;

using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.ViewModels;

public partial class ReviewOptionItem : ViewModelBase
{
    public string OptionText { get; set; } = "";
    /// <summary> Its key, 1-9, or blank past nine. </summary>
    public string NumberText { get; init; } = "";
    public bool IsCorrect { get; set; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary> A right-hand item of a match card, placed against a left item by clicking it or pressing its number. </summary>
public partial class ReviewMatchChip : ViewModelBase
{
    public string Text { get; init; } = "";
    /// <summary> Its key, 1-9, or blank past nine. </summary>
    public string NumberText { get; init; } = "";

    [ObservableProperty] private bool _isUsed;
}

public partial class ReviewMatchRow : ViewModelBase
{
    public string LeftText { get; set; } = "";
    public string CorrectRightText { get; set; } = "";

    /// <summary> The row the next chip goes in. </summary>
    [ObservableProperty] private bool _isCurrent;
    [NotifyPropertyChangedFor(nameof(SelectedRightText))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [ObservableProperty] private ReviewMatchChip? _selectedChip;
    [ObservableProperty] private bool _isCorrect;

    public string? SelectedRightText => SelectedChip?.Text;
    public bool HasSelection => SelectedChip is not null;
}

public partial class ReviewViewModel : ViewModelBase
    , IDisposable
{
    private readonly List<FlashCard> _sessionCards;
    private readonly Dictionary<ulong, ulong>? _cardDeckMap;
    private readonly ulong? _reviewGroupId;
    /// <summary> Deck names by ID, for labelling questions in a study group review. </summary>
    private readonly Dictionary<ulong, string>? _deckNames;
    private readonly Dictionary<ulong, int> _attemptsByDeck = [];
    private readonly Dictionary<ulong, int> _correctByDeck = [];
    private int _currentIndex = 0;
    private Stopwatch _timer = new();
    private Timer? _displayTimer;
    private readonly ulong deckID = ulong.MaxValue;
    private bool _currentCardHasBeenScored;
    private bool _disposed;

    public FlashCard CurrentCard => _sessionCards[_currentIndex];
    public int TotalCards => _sessionCards.Count;
    public int CurrentNumber => _currentIndex + 1;
    public int QuestionsAnsweredSoFar => _currentIndex + 1;

    // Scoring
    public int CorrectCount { get; private set; } = 0;
    [NotifyPropertyChangedFor(nameof(CurrentAnswerStreakText))]
    [ObservableProperty] private int _currentAnswerStreak = 0;
    [NotifyPropertyChangedFor(nameof(BestAnswerStreakText))]
    [ObservableProperty] private int _bestAnswerStreak = 0;
    [NotifyPropertyChangedFor(nameof(ShowAnswerButtonVisible))]
    [NotifyPropertyChangedFor(nameof(ShowBackAnswer))]
    [NotifyPropertyChangedFor(nameof(ShowBackSection))]
    [NotifyPropertyChangedFor(nameof(CanRetryLater))]
    [NotifyPropertyChangedFor(nameof(ShowFlipRetryLater))]
    [NotifyPropertyChangedFor(nameof(KeyboardHint))]
    [ObservableProperty] private bool _isAnswerRevealed = false;
    [ObservableProperty] private string _userTypedAnswer = "";
    public Action<int, int, TimeSpan, bool> OnSessionComplete = delegate { };

    public bool IsTypeCard => CurrentCard is TypeFlashCard;
    public bool IsFlipCard => CurrentCard is FlipFlashCard;
    public bool IsReversedCard => CurrentCard is FlipFlashCard { IsReversedCopy: true };
    public bool IsMultiChoiceCard => CurrentCard is MultiFlashCard;
    public bool IsMatchCard => CurrentCard is MatchFlashCard;
    public bool IsTrueFalseCard => CurrentCard is TrueFalseFlashCard;
    public string CurrentTypeCardAnswer => CurrentCard is TypeFlashCard typeCard ? typeCard.Answer : CurrentCard.Back;
    public string CurrentTrueFalseTrueOptionText => CurrentCard is TrueFalseFlashCard trueFalseCard
        ? trueFalseCard.TrueLabel
        : "True";
    public string CurrentTrueFalseFalseOptionText => CurrentCard is TrueFalseFlashCard trueFalseCard
        ? trueFalseCard.FalseLabel
        : "False";
    public string CurrentTrueFalseCorrectOptionText => CurrentCard is TrueFalseFlashCard trueFalseCard
        ? (trueFalseCard.CorrectAnswerIsTrue ? trueFalseCard.TrueLabel : trueFalseCard.FalseLabel)
        : "";
    public bool ShowBackAnswer => IsAnswerRevealed;
    /// <summary> The back once answered, unless there's nothing to show (blank, or a match card's placeholder). </summary>
    public bool ShowBackSection => IsAnswerRevealed && !IsMatchCard && !string.IsNullOrWhiteSpace(CurrentCard.Back);
    [NotifyPropertyChangedFor(nameof(CanRetryLater))]
    [NotifyPropertyChangedFor(nameof(KeyboardHint))]
    [ObservableProperty] private bool _isAnswerChecked = false;
    public bool ShowAnswerButtonVisible => IsFlipCard && !IsAnswerRevealed;
    public ObservableCollection<ReviewOptionItem> MultiChoiceAnswerOptions { get; } = new();
    public ObservableCollection<ReviewMatchRow> MatchRows { get; } = new();
    public ObservableCollection<ReviewMatchChip> MatchChips { get; } = new();

    public bool HasSelectedWrongOptions => SelectedWrongOptions.Count > 0;
    public bool HasMissedCorrectOptions => MissedCorrectOptions.Count > 0;

    public ObservableCollection<string> SelectedWrongOptions { get; } = new();
    public ObservableCollection<string> MissedCorrectOptions { get; } = new();
    
    [ObservableProperty] private bool _isAnswerCorrect = false;

    [ObservableProperty] private string _timerText = "0:00:00";

    public bool ShouldShowTimer => MetaDataManager.Data.ShowTimer;
    public bool ShouldShowProgress => MetaDataManager.Data.ShowProgress;
    public bool ShouldShowSkipButton => MetaDataManager.Data.ShowSkipButton;
    public bool ShouldShowRetryLaterButton => MetaDataManager.Data.ShowRetryLaterButton;
    public bool CanRetryLater => MetaDataManager.Data.ShowRetryLaterButton && (IsAnswerChecked || (IsFlipCard && IsAnswerRevealed));
    public bool ShowFlipRetryLater => IsFlipCard && CanRetryLater;
    public bool ShouldShowAnswerStreak => MetaDataManager.Data.ShowAnswerStreakInReview;
    public string CurrentAnswerStreakText => $"{CurrentAnswerStreak} in a row";
    public string BestAnswerStreakText => $"Best: {BestAnswerStreak}";

    public int ProgressPercentage => TotalCards > 0 ? (CurrentNumber * 100) / TotalCards : 0;
    public string ProgressCardCount => $"{CurrentNumber}/{TotalCards}";

    /// <summary> The question's type, after its deck's name in a study group review. </summary>
    public string CardLabel
    {
        get
        {
            var type = CurrentCard switch
            {
                { ReviewLabel: { } label } => label,
                FlipFlashCard { IsReversedCopy: true } => $"{CARD_TYPE_FLIP} · Reversed",
                FlipFlashCard => CARD_TYPE_FLIP,
                _ => CurrentCard.CardType,
            };

            return _deckNames is not null && _cardDeckMap!.TryGetValue(CurrentCard.ID, out var deckId)
                && _deckNames.TryGetValue(deckId, out var deckName)
                ? $"{deckName} · {type}"
                : type;
        }
    }

    /// <summary> The keyboard shortcuts that do something right now, shown under the card. </summary>
    public string KeyboardHint
    {
        get
        {
            const string gap = "      ";
            if (IsAnswerChecked) return CanRetryLater ? $"Enter: next card{gap}R: retry later" : "Enter: next card";

            var hint = this switch
            {
                { IsFlipCard: true, IsAnswerRevealed: false } => "Space: show answer",
                { IsFlipCard: true } => $"←  Incorrect{gap}→  Correct" + (CanRetryLater ? $"{gap}R: retry later" : ""),
                { IsTrueFalseCard: true } => $"←  {CurrentTrueFalseTrueOptionText}{gap}→  {CurrentTrueFalseFalseOptionText}",
                { IsMultiChoiceCard: true } => $"1–{Math.Min(MultiChoiceAnswerOptions.Count, 9)}: tick an option{gap}Enter: submit",
                { IsMatchCard: true } => $"1–{Math.Min(MatchChips.Count, 9)}: place an answer{gap}↑ ↓: move{gap}Backspace: undo{gap}Enter: submit",
                _ => "Enter: submit",
            };

            var canSkip = ShouldShowSkipButton && TotalCards > 1 && _currentIndex < TotalCards - 1;
            return canSkip && !(IsFlipCard && IsAnswerRevealed) ? $"{hint}{gap}S: skip" : hint;
        }
    }

    /// <summary> Ticks or unticks the multiple choice option at <paramref name="index"/> (0-based). </summary>
    public void ToggleMultiChoiceOption(int index)
    {
        if (!IsMultiChoiceCard || IsAnswerChecked || index < 0 || index >= MultiChoiceAnswerOptions.Count) return;
        MultiChoiceAnswerOptions[index].IsSelected = !MultiChoiceAnswerOptions[index].IsSelected;
    }

    public ReviewViewModel(IEnumerable<FlashCard> cards, ulong deckID, Dictionary<ulong, ulong>? cardDeckMap = null, ulong? reviewGroupId = null)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // Reversible flip cards are also asked back to front, and cloze cards once per blank group:
        // each is a question of its own.
        _sessionCards = [.. cards
            .SelectMany(card => card switch
            {
                FlipFlashCard { IsReversible: true } flip => [card, flip.CreateReversedCopy()],
                ClozeFlashCard cloze => cloze.CreateQuestions(),
                _ => [card],
            })
            .OrderBy(_ => Guid.NewGuid())]; // Shuffle cards
        if (_sessionCards.Count == 0)
        {
            throw new ArgumentException("Cannot start a review session with no cards.", nameof(cards));
        }

        _timer.Start();
        this.deckID = deckID;
        _cardDeckMap = cardDeckMap;
        if (cardDeckMap is not null)
            _deckNames = FlashCardRepository.GetAllDecks().ToDictionary(deck => deck.ID, deck => deck.Name);
        _reviewGroupId = reviewGroupId;

        MetaDataManager.Data.PropertyChanged += Settings_PropertyChanged;
        RefreshBestAnswerStreak();

        LoadMultiChoiceOptionsForCurrentCard();
        LoadMatchRowsForCurrentCard();

        // Start a timer to update the display every 100ms
        _displayTimer = new Timer(100);
        _displayTimer.Elapsed += (_, _) =>
        {
            if (ShouldShowTimer)
            {
                UpdateTimerText();
            }
        };
        _displayTimer.AutoReset = true;
        _displayTimer.Start();

        // Initialize timer text
        UpdateTimerText();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppMetaData.ShowTimer):
                OnPropertyChanged(nameof(ShouldShowTimer));
                break;
            case nameof(AppMetaData.ShowProgress):
                OnPropertyChanged(nameof(ShouldShowProgress));
                break;
            case nameof(AppMetaData.ShowSkipButton):
                OnPropertyChanged(nameof(ShouldShowSkipButton));
                break;
            case nameof(AppMetaData.ShowRetryLaterButton):
                OnPropertyChanged(nameof(ShouldShowRetryLaterButton));
                OnPropertyChanged(nameof(CanRetryLater));
                OnPropertyChanged(nameof(ShowFlipRetryLater));
                break;
            case nameof(AppMetaData.ShowAnswerStreakInReview):
                OnPropertyChanged(nameof(ShouldShowAnswerStreak));
                break;
        }
    }

    private void UpdateTimerText()
    {
        var elapsed = _timer.Elapsed;
        TimerText = $"{elapsed.Hours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    public void Reveal()
    {
        IsAnswerRevealed = true;
    }

    public void MarkCorrect()
    {
        RecordCurrentCardResult(true);
        NextCard();
    }

    public void MarkIncorrect()
    {
        RecordCurrentCardResult(false);
        NextCard();
    }

    public void CheckTypedAnswer()
    {
        IsAnswerCorrect = CurrentCard.VerifyAnswer(UserTypedAnswer);
        RecordCurrentCardResult(IsAnswerCorrect);
        IsAnswerChecked = true;
        IsAnswerRevealed = true;
    }

    public void CheckMultiChoiceAnswer()
    {
        if (CurrentCard is not MultiFlashCard)
        {
            return;
        }

        var selectedAnswers = MultiChoiceAnswerOptions
            .Where(o => o.IsSelected)
            .Select(o => o.OptionText)
            .ToList();

        IsAnswerCorrect = CurrentCard.VerifyAnswer(selectedAnswers);
        RecordCurrentCardResult(IsAnswerCorrect);

        SelectedWrongOptions.Clear();
        MissedCorrectOptions.Clear();

        foreach (var option in MultiChoiceAnswerOptions)
        {
            if (option.IsSelected && !option.IsCorrect)
            {
                SelectedWrongOptions.Add(option.OptionText);
            }

            if (!option.IsSelected && option.IsCorrect)
            {
                MissedCorrectOptions.Add(option.OptionText);
            }
        }

        IsAnswerChecked = true;
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(HasSelectedWrongOptions));
        OnPropertyChanged(nameof(HasMissedCorrectOptions));
    }

    public void CheckMatchAnswer()
    {
        if (CurrentCard is not MatchFlashCard)
        {
            return;
        }

        var selectedPairs = MatchRows
            .Where(row => !string.IsNullOrWhiteSpace(row.SelectedRightText))
            .Select(row => (row.LeftText, rightText: row.SelectedRightText!))
            .ToList();

        IsAnswerCorrect = CurrentCard.VerifyAnswer(selectedPairs);
        RecordCurrentCardResult(IsAnswerCorrect);

        foreach (var row in MatchRows)
        {
            row.IsCorrect = string.Equals(row.SelectedRightText, row.CorrectRightText, StringComparison.Ordinal);
            row.IsCurrent = false;
        }

        IsAnswerChecked = true;
        IsAnswerRevealed = true;
    }

    public void CheckTrueFalseAnswer(bool selectedAnswerIsTrue)
    {
        if (CurrentCard is not TrueFalseFlashCard trueFalseCard)
        {
            return;
        }

        IsAnswerCorrect = trueFalseCard.VerifyAnswer(selectedAnswerIsTrue);
        RecordCurrentCardResult(IsAnswerCorrect);

        IsAnswerChecked = true;
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(CurrentTrueFalseCorrectOptionText));
    }

    public void NextCard()
    {
        if (_currentIndex < _sessionCards.Count - 1)
        {
            _currentIndex++;
            ResetForCurrentCard();
        }
        else
        {
            CompleteSession();
        }
    }

    public void SkipCard()
    {
        if (!MetaDataManager.Data.ShowSkipButton || _sessionCards.Count <= 1 || _currentIndex >= _sessionCards.Count - 1)
        {
            return;
        }

        SkipCurrentCard();
        ResetForCurrentCard();
    }

    public void RetryLater()
    {
        if (!MetaDataManager.Data.ShowRetryLaterButton || _sessionCards.Count <= 1)
        {
            return;
        }

        if (!IsAnswerChecked && !(IsFlipCard && IsAnswerRevealed))
        {
            return;
        }

        MoveCurrentCardToEnd();
        ResetForCurrentCard();
    }

    public void QuitSession()
    {
        _timer.Stop();
        Dispose();
        CompleteSession(isPartial: true);
    }

    private void CompleteSession(bool isPartial = false)
    {
        _timer.Stop();
        Dispose();

        int elapsedSeconds = (int)Math.Round(_timer.Elapsed.TotalSeconds);
        var totalAttempts = _attemptsByDeck.Values.Sum();
        int questionsAttempted = totalAttempts;

        if (totalAttempts > 0)
        {
            var deckResults = _attemptsByDeck
                .Where(kvp => kvp.Value > 0)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            int distributedSeconds = 0;
            for (int i = 0; i < deckResults.Count; i++)
            {
                var (targetDeckId, attempts) = deckResults[i];
                int correct = _correctByDeck.GetValueOrDefault(targetDeckId);

                int deckSeconds = i == deckResults.Count - 1
                    ? elapsedSeconds - distributedSeconds
                    : (int)((long)elapsedSeconds * attempts / totalAttempts);

                distributedSeconds += deckSeconds;
                FlashCardRepository.UpdateDeckStats(targetDeckId, correct, attempts, deckSeconds);
            }
        }

        OnSessionComplete?.Invoke(CorrectCount, questionsAttempted, _timer.Elapsed, isPartial);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MetaDataManager.Data.PropertyChanged -= Settings_PropertyChanged;
        _displayTimer?.Stop();
        _displayTimer?.Dispose();
        _displayTimer = null;
    }

    private void RecordCurrentCardResult(bool isCorrect)
    {
        if (_currentCardHasBeenScored)
        {
            return;
        }

        _currentCardHasBeenScored = true;
        UpdateAnswerStreak(isCorrect);
        ulong targetDeckId = GetCurrentCardDeckId();

        _attemptsByDeck[targetDeckId] = _attemptsByDeck.GetValueOrDefault(targetDeckId) + 1;
        if (isCorrect)
        {
            CorrectCount++;
            _correctByDeck[targetDeckId] = _correctByDeck.GetValueOrDefault(targetDeckId) + 1;
        }
    }

    private void UpdateAnswerStreak(bool isCorrect)
    {
        CurrentAnswerStreak = isCorrect ? CurrentAnswerStreak + 1 : 0;

        var (targetType, targetId) = GetBestStreakTarget();
        if (CurrentAnswerStreak > BestAnswerStreak)
        {
            BestAnswerStreak = CurrentAnswerStreak;
            FlashCardRepository.UpdateBestAnswerStreak(targetType, targetId, BestAnswerStreak);
        }
    }

    private void RefreshBestAnswerStreak()
    {
        var (targetType, targetId) = GetBestStreakTarget();
        BestAnswerStreak = FlashCardRepository.GetBestAnswerStreak(targetType, targetId);
    }

    private (string targetType, ulong targetId) GetBestStreakTarget()
    {
        if (_reviewGroupId.HasValue)
        {
            return ("Group", _reviewGroupId.Value);
        }

        return ("Deck", GetCurrentCardDeckId());
    }

    private ulong GetCurrentCardDeckId()
    {
        if (_cardDeckMap is not null
            && CurrentCard.ID != ulong.MaxValue
            && _cardDeckMap.TryGetValue(CurrentCard.ID, out var mappedDeckId))
        {
            return mappedDeckId;
        }

        return deckID;
    }

    private void LoadMultiChoiceOptionsForCurrentCard()
    {
        MultiChoiceAnswerOptions.Clear();

        if (CurrentCard is not MultiFlashCard multiCard)
        {
            return;
        }

        foreach (var (optionText, isCorrect) in multiCard.Options.OrderBy(_ => Guid.NewGuid()))
        {
            var number = MultiChoiceAnswerOptions.Count + 1;
            MultiChoiceAnswerOptions.Add(new ReviewOptionItem
            {
                OptionText = optionText,
                NumberText = number <= 9 ? $"{number}" : "",
                IsCorrect = isCorrect,
                IsSelected = false,
            });
        }
    }

    private void LoadMatchRowsForCurrentCard()
    {
        MatchRows.Clear();
        MatchChips.Clear();

        if (CurrentCard is not MatchFlashCard matchCard)
        {
            return;
        }

        var randomizedPairs = matchCard.Options.OrderBy(_ => Guid.NewGuid()).ToList();
        var randomizedRightChoices = randomizedPairs
            .Select(p => p.rightText)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        for (var i = 0; i < randomizedRightChoices.Count; i++)
        {
            MatchChips.Add(new ReviewMatchChip { Text = randomizedRightChoices[i], NumberText = i < 9 ? $"{i + 1}" : "" });
        }

        foreach (var (leftText, rightText) in randomizedPairs)
        {
            MatchRows.Add(new ReviewMatchRow { LeftText = leftText, CorrectRightText = rightText });
        }

        if (MatchRows.Count > 0) MatchRows[0].IsCurrent = true;
    }

    // --- Match cards: chips go into the current row, which then moves to the next empty one ---

    private ReviewMatchRow? CurrentMatchRow => MatchRows.FirstOrDefault(row => row.IsCurrent);

    /// <summary> Puts the chip at <paramref name="index"/> (0-based) in the current row. </summary>
    public void PlaceMatchChip(int index)
    {
        if (index >= 0 && index < MatchChips.Count) PlaceMatchChip(MatchChips[index]);
    }

    public void PlaceMatchChip(ReviewMatchChip chip)
    {
        if (!IsMatchCard || IsAnswerChecked || chip.IsUsed || CurrentMatchRow is not { } row) return;

        row.SelectedChip?.IsUsed = false;
        row.SelectedChip = chip;
        chip.IsUsed = true;

        // On to the next empty row after this one, wrapping round; stay put once every row is filled.
        var start = MatchRows.IndexOf(row);
        var next = Enumerable.Range(1, MatchRows.Count)
            .Select(offset => MatchRows[(start + offset) % MatchRows.Count])
            .FirstOrDefault(candidate => !candidate.HasSelection);
        if (next is not null) SetCurrentMatchRow(next);
    }

    /// <summary> Makes <paramref name="row"/> current, taking out the chip it had so another can go in. </summary>
    public void SelectMatchRow(ReviewMatchRow row)
    {
        if (!IsMatchCard || IsAnswerChecked) return;
        ClearMatchRow(row);
        SetCurrentMatchRow(row);
    }

    /// <summary> Moves the current row up (-1) or down (+1). </summary>
    public void MoveMatchRow(int delta)
    {
        if (!IsMatchCard || IsAnswerChecked || CurrentMatchRow is not { } row) return;
        var index = Math.Clamp(MatchRows.IndexOf(row) + delta, 0, MatchRows.Count - 1);
        SetCurrentMatchRow(MatchRows[index]);
    }

    /// <summary> Takes the chip out of the current row, or if that's empty, out of the row above and moves there. </summary>
    public void UndoMatchChip()
    {
        if (!IsMatchCard || IsAnswerChecked || CurrentMatchRow is not { } row) return;
        if (!row.HasSelection && MatchRows.IndexOf(row) > 0)
        {
            row = MatchRows[MatchRows.IndexOf(row) - 1];
            SetCurrentMatchRow(row);
        }
        ClearMatchRow(row);
    }

    private static void ClearMatchRow(ReviewMatchRow row)
    {
        row.SelectedChip?.IsUsed = false;
        row.SelectedChip = null;
    }

    private void SetCurrentMatchRow(ReviewMatchRow current)
    {
        foreach (var row in MatchRows) row.IsCurrent = row == current;
    }

    private void MoveCurrentCardToEnd()
    {
        var current = _sessionCards[_currentIndex];
        _sessionCards.RemoveAt(_currentIndex);
        _sessionCards.Add(current);

        if (_currentIndex >= _sessionCards.Count)
        {
            _currentIndex = _sessionCards.Count - 1;
        }
    }

    private void SkipCurrentCard()
    {
        _sessionCards.RemoveAt(_currentIndex);
    }

    private void ResetForCurrentCard()
    {
        IsAnswerRevealed = false;
        IsAnswerChecked = false;
        IsAnswerCorrect = false;
        UserTypedAnswer = "";
        _currentCardHasBeenScored = false;
        SelectedWrongOptions.Clear();
        MissedCorrectOptions.Clear();
        LoadMultiChoiceOptionsForCurrentCard();
        LoadMatchRowsForCurrentCard();
        RefreshBestAnswerStreak();
        OnPropertyChanged(nameof(CurrentCard));
        OnPropertyChanged(nameof(CurrentNumber));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressCardCount));
        OnPropertyChanged(nameof(CardLabel));
        OnPropertyChanged(nameof(KeyboardHint));
        OnPropertyChanged(nameof(IsTypeCard));
        OnPropertyChanged(nameof(IsFlipCard));
        OnPropertyChanged(nameof(ShowFlipRetryLater));
        OnPropertyChanged(nameof(IsReversedCard));
        OnPropertyChanged(nameof(IsMultiChoiceCard));
        OnPropertyChanged(nameof(IsMatchCard));
        OnPropertyChanged(nameof(IsTrueFalseCard));
        OnPropertyChanged(nameof(CurrentTypeCardAnswer));
        OnPropertyChanged(nameof(CurrentTrueFalseTrueOptionText));
        OnPropertyChanged(nameof(CurrentTrueFalseFalseOptionText));
        OnPropertyChanged(nameof(CurrentTrueFalseCorrectOptionText));
        OnPropertyChanged(nameof(ShowAnswerButtonVisible));
        OnPropertyChanged(nameof(HasSelectedWrongOptions));
        OnPropertyChanged(nameof(HasMissedCorrectOptions));
    }

}