using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ReviFlash.Data;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

public class ReviewViewModel : ViewModelBase
{
    private readonly List<FlashCard> _sessionCards;
    private int _currentIndex = 0;
    private Stopwatch _timer = new();

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
    public bool IsAnswerChecked { get; set; } = false;
    public bool ShowAnswerButtonVisible => IsFlipCard && !IsAnswerRevealed;
    
    private bool _isAnswerCorrect = false;
    public bool IsAnswerCorrect
    {
        get => _isAnswerCorrect;
        set { _isAnswerCorrect = value; OnPropertyChanged(nameof(IsAnswerCorrect)); }
    }

    private readonly ulong deckID = ulong.MaxValue;

    public ReviewViewModel(IEnumerable<FlashCard> cards, ulong deckID)
    {
        _sessionCards = [.. cards];
        _timer.Start();
        this.deckID = deckID;
    }

    public void Reveal()
    {
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(ShowAnswerButtonVisible));
    }

    public void MarkCorrect()
    {
        CorrectCount++;
        NextCard();
    }

    public void MarkIncorrect() => NextCard();

    public void CheckTypedAnswer()
    {
        IsAnswerCorrect = CurrentCard.VerifyAnswer(UserTypedAnswer);
        if (IsAnswerCorrect)
        {
            CorrectCount++;
        }
        IsAnswerChecked = true;
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(IsAnswerRevealed));
        OnPropertyChanged(nameof(IsAnswerChecked));
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
            OnPropertyChanged(nameof(CurrentCard));
            OnPropertyChanged(nameof(IsAnswerRevealed));
            OnPropertyChanged(nameof(IsAnswerChecked));
            OnPropertyChanged(nameof(UserTypedAnswer));
            OnPropertyChanged(nameof(CurrentNumber));
            OnPropertyChanged(nameof(IsTypeCard));
            OnPropertyChanged(nameof(IsFlipCard));
            OnPropertyChanged(nameof(ShowAnswerButtonVisible));
        }
        else
        {
            CompleteSession();
        }
    }

    public void QuitSession()
    {
        _timer.Stop();
        CompleteSession(isPartial: true);
    }

    private void CompleteSession(bool isPartial = false)
    {
        _timer.Stop();
        int elapsedSeconds = (int)Math.Round(_timer.Elapsed.TotalSeconds);
        // When quitting early, count only the cards actually answered (_currentIndex cards have been answered)
        // When completing normally, count all cards
        int questionsAttempted = isPartial ? _currentIndex : TotalCards;
        FlashCardRepository.UpdateDeckStats(deckID, CorrectCount, questionsAttempted, elapsedSeconds);
        OnSessionComplete?.Invoke(CorrectCount, questionsAttempted, _timer.Elapsed, isPartial);
    }
}