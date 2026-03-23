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

    // Scoring
    public int CorrectCount { get; private set; } = 0;
    public bool IsAnswerRevealed { get; set; } = false;
    public string UserTypedAnswer { get; set; } = "";
    public Action<int, int, TimeSpan> OnSessionComplete = delegate { };

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
    }

    public void MarkCorrect()
    {
        CorrectCount++;
        NextCard();
    }

    public void MarkIncorrect() => NextCard();

    public void CheckTypedAnswer()
    {
        // Simple string comparison for the "Type" card
        if (UserTypedAnswer.Trim().ToLower() == CurrentCard.Back.Trim().ToLower())
        {
            CorrectCount++;
        }
        IsAnswerRevealed = true;
        OnPropertyChanged(nameof(IsAnswerRevealed));
    }

    private void NextCard()
    {
        if (_currentIndex < _sessionCards.Count - 1)
        {
            _currentIndex++;
            IsAnswerRevealed = false;
            UserTypedAnswer = "";
            OnPropertyChanged(nameof(CurrentCard));
            OnPropertyChanged(nameof(IsAnswerRevealed));
            OnPropertyChanged(nameof(UserTypedAnswer));
            OnPropertyChanged(nameof(CurrentNumber));
        }
        else
        {
            _timer.Stop();
            FlashCardRepository.UpdateDeckStats(deckID, CorrectCount, TotalCards);
            OnSessionComplete?.Invoke(CorrectCount, TotalCards, _timer.Elapsed);
        }
    }
}