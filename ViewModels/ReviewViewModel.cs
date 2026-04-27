using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using ReviFlash.Data;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

public class ReviewOptionItem : ViewModelBase
{
    private bool _isSelected;

    public string OptionText { get; set; } = "";
    public bool IsCorrect { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
}

public class ReviewMatchRow : ViewModelBase
{
    private string? _selectedRightText;

    public string LeftText { get; set; } = "";
    public string CorrectRightText { get; set; } = "";
    public List<string> RightChoices { get; set; } = [];

    public string? SelectedRightText
    {
        get => _selectedRightText;
        set
        {
            _selectedRightText = value;
            OnPropertyChanged(nameof(SelectedRightText));
        }
    }
}

public class ReviewViewModel : ViewModelBase
{
    private readonly List<FlashCard> _sessionCards;
    private readonly Dictionary<ulong, ulong>? _cardDeckMap;
    private readonly Dictionary<ulong, int> _attemptsByDeck = [];
    private readonly Dictionary<ulong, int> _correctByDeck = [];
    private int _currentIndex = 0;
    private Stopwatch _timer = new();
    private Timer? _displayTimer;
    private readonly ulong deckID = ulong.MaxValue;
    private bool _currentCardHasBeenScored;

    public FlashCard CurrentCard => _sessionCards[_currentIndex];
    public int TotalCards => _sessionCards.Count;
    public int CurrentNumber => _currentIndex + 1;
    public int QuestionsAnsweredSoFar => _currentIndex + 1;

    // Scoring
    public int CorrectCount { get; private set; } = 0;
    public bool IsAnswerRevealed { get; set; } = false;
    public string UserTypedAnswer { get; set; } = "";
    public Action<int, int, TimeSpan, bool> OnSessionComplete = delegate { };

    public bool IsTypeCard => CurrentCard is TypeFlashCard;
    public bool IsFlipCard => CurrentCard is FlipFlashCard;
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
    public bool IsAnswerChecked { get; set; } = false;
    public bool ShowAnswerButtonVisible => IsFlipCard && !IsAnswerRevealed;
    public ObservableCollection<ReviewOptionItem> MultiChoiceAnswerOptions { get; } = new();
    public ObservableCollection<ReviewMatchRow> MatchRows { get; } = new();
    public ObservableCollection<string> MatchRightChoices { get; } = new();

    public bool HasSelectedWrongOptions => SelectedWrongOptions.Count > 0;
    public bool HasMissedCorrectOptions => MissedCorrectOptions.Count > 0;
    public bool HasWrongMatches => WrongMatches.Count > 0;

    public ObservableCollection<string> SelectedWrongOptions { get; } = new();
    public ObservableCollection<string> MissedCorrectOptions { get; } = new();
    public ObservableCollection<string> WrongMatches { get; } = new();
    
    private bool _isAnswerCorrect = false;
    public bool IsAnswerCorrect
    {
        get => _isAnswerCorrect;
        set { _isAnswerCorrect = value; OnPropertyChanged(nameof(IsAnswerCorrect)); }
    }

    private string _timerText = "0:00:00";
    public string TimerText
    {
        get => _timerText;
        set { _timerText = value; OnPropertyChanged(nameof(TimerText)); }
    }

    public bool ShouldShowTimer => App.CurrentMetaData.ShowTimer;
    public bool ShouldShowProgress => App.CurrentMetaData.ShowProgress;

    public int ProgressPercentage => TotalCards > 0 ? (CurrentNumber * 100) / TotalCards : 0;
    public string ProgressCardCount => $"{CurrentNumber}/{TotalCards}";

    public ReviewViewModel(IEnumerable<FlashCard> cards, ulong deckID, Dictionary<ulong, ulong>? cardDeckMap = null)
    {
        _sessionCards = [.. cards.OrderBy(_ => Guid.NewGuid()).ToList()]; // Shuffle cards
        _timer.Start();
        this.deckID = deckID;
        _cardDeckMap = cardDeckMap;

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

    private void UpdateTimerText()
    {
        var elapsed = _timer.Elapsed;
        TimerText = $"{elapsed.Hours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    public void Reveal()
    {
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(ShowAnswerButtonVisible));
        OnPropertyChanged(nameof(ShowBackAnswer));
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
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(IsAnswerChecked));
        OnPropertyChanged(nameof(ShowBackAnswer));
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
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(IsAnswerChecked));
        OnPropertyChanged(nameof(ShowBackAnswer));
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

        WrongMatches.Clear();
        foreach (var row in MatchRows)
        {
            if (!string.Equals(row.SelectedRightText, row.CorrectRightText, StringComparison.Ordinal))
            {
                var selected = string.IsNullOrWhiteSpace(row.SelectedRightText) ? "(no selection)" : row.SelectedRightText;
                WrongMatches.Add($"{row.LeftText} -> {selected} (correct: {row.CorrectRightText})");
            }
        }

        IsAnswerChecked = true;
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(IsAnswerChecked));
        OnPropertyChanged(nameof(ShowBackAnswer));
        OnPropertyChanged(nameof(HasWrongMatches));
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
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(IsAnswerChecked));
        OnPropertyChanged(nameof(IsAnswerCorrect));
        OnPropertyChanged(nameof(CurrentTrueFalseCorrectOptionText));
        OnPropertyChanged(nameof(ShowBackAnswer));
    }

    public void NextCard()
    {
        if (_currentIndex < _sessionCards.Count - 1)
        {
            _currentIndex++;
            IsAnswerRevealed = false;
            IsAnswerChecked = false;
            IsAnswerCorrect = false;
            UserTypedAnswer = "";
            _currentCardHasBeenScored = false;
            SelectedWrongOptions.Clear();
            MissedCorrectOptions.Clear();
            WrongMatches.Clear();
            LoadMultiChoiceOptionsForCurrentCard();
            LoadMatchRowsForCurrentCard();
            OnPropertyChanged(nameof(CurrentCard));
            OnPropertyChanged(nameof(IsAnswerRevealed));
            OnPropertyChanged(nameof(IsAnswerChecked));
            OnPropertyChanged(nameof(UserTypedAnswer));
            OnPropertyChanged(nameof(CurrentNumber));
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(ProgressCardCount));
            OnPropertyChanged(nameof(IsTypeCard));
            OnPropertyChanged(nameof(IsFlipCard));
            OnPropertyChanged(nameof(IsMultiChoiceCard));
            OnPropertyChanged(nameof(IsMatchCard));
            OnPropertyChanged(nameof(IsTrueFalseCard));
            OnPropertyChanged(nameof(CurrentTypeCardAnswer));
            OnPropertyChanged(nameof(CurrentTrueFalseTrueOptionText));
            OnPropertyChanged(nameof(CurrentTrueFalseFalseOptionText));
            OnPropertyChanged(nameof(CurrentTrueFalseCorrectOptionText));
            OnPropertyChanged(nameof(ShowBackAnswer));
            OnPropertyChanged(nameof(ShowAnswerButtonVisible));
            OnPropertyChanged(nameof(HasSelectedWrongOptions));
            OnPropertyChanged(nameof(HasMissedCorrectOptions));
            OnPropertyChanged(nameof(HasWrongMatches));
        }
        else
        {
            CompleteSession();
        }
    }

    public void QuitSession()
    {
        _timer.Stop();
        _displayTimer?.Stop();
        _displayTimer?.Dispose();
        CompleteSession(isPartial: true);
    }

    private void CompleteSession(bool isPartial = false)
    {
        _timer.Stop();
        _displayTimer?.Stop();
        _displayTimer?.Dispose();

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

    private void RecordCurrentCardResult(bool isCorrect)
    {
        if (_currentCardHasBeenScored)
        {
            return;
        }

        _currentCardHasBeenScored = true;
        ulong targetDeckId = GetCurrentCardDeckId();

        _attemptsByDeck[targetDeckId] = _attemptsByDeck.GetValueOrDefault(targetDeckId) + 1;
        if (isCorrect)
        {
            CorrectCount++;
            _correctByDeck[targetDeckId] = _correctByDeck.GetValueOrDefault(targetDeckId) + 1;
        }
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
            MultiChoiceAnswerOptions.Add(new ReviewOptionItem
            {
                OptionText = optionText,
                IsCorrect = isCorrect,
                IsSelected = false,
            });
        }
    }

    private void LoadMatchRowsForCurrentCard()
    {
        MatchRows.Clear();
        MatchRightChoices.Clear();

        if (CurrentCard is not MatchFlashCard matchCard)
        {
            return;
        }

        var randomizedPairs = matchCard.Options.OrderBy(_ => Guid.NewGuid()).ToList();
        var randomizedRightChoices = randomizedPairs
            .Select(p => p.rightText)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        foreach (var choice in randomizedRightChoices)
        {
            MatchRightChoices.Add(choice);
        }

        foreach (var (leftText, rightText) in randomizedPairs)
        {
            MatchRows.Add(new ReviewMatchRow
            {
                LeftText = leftText,
                CorrectRightText = rightText,
                RightChoices = [.. randomizedRightChoices],
                SelectedRightText = null,
            });
        }
    }

}