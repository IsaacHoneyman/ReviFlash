using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> Makes a card from a note: the selection becomes the back (or cloze text) and the nearest heading the front. </summary>
public partial class MakeCardViewModel : ViewModelBase
{
    public const string FlipType = "Flip";
    public const string ClozeType = "Cloze";

    // Remembered between cards, since notes usually turn into several cards for the same set.
    private static string _lastCardType = FlipType;
    private static ulong? _lastDeckID;

    public IReadOnlyList<string> CardTypes { get; } = [FlipType, ClozeType];

    [NotifyPropertyChangedFor(nameof(IsFlip))]
    [NotifyPropertyChangedFor(nameof(IsCloze))]
    [ObservableProperty] private string _selectedCardType;

    public bool IsFlip => SelectedCardType == FlipType;
    public bool IsCloze => SelectedCardType == ClozeType;

    [ObservableProperty] private string _front;
    [ObservableProperty] private string _back;
    [ObservableProperty] private string _clozeText;
    [ObservableProperty] private string _extra = "";

    [NotifyPropertyChangedFor(nameof(SelectedDeckText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedDeck))]
    [ObservableProperty] private FlashCardDeck? _selectedDeck;

    public bool HasSelectedDeck => SelectedDeck is not null;
    public string SelectedDeckText => SelectedDeck is { } deck ? deck.Name : "Choose a set below";

    [NotifyPropertyChangedFor(nameof(HasError))]
    [ObservableProperty] private string _errorText = "";
    public bool HasError => ErrorText.Length > 0;

    public DeckFolderBrowser DeckBrowser { get; }

    public MakeCardViewModel(string selection, string? heading)
    {
        _selectedCardType = _lastCardType;
        _front = heading ?? "";
        _back = selection;
        _clozeText = selection;

        DeckBrowser = new DeckFolderBrowser("Choose", deck => SelectedDeck = deck) { ShowSortOptions = false };
        var decks = FlashCardRepository.GetAllDecks();
        DeckBrowser.SetDecks(decks);
        _selectedDeck = decks.Find(deck => deck.ID == _lastDeckID);
    }

    /// <summary> Saves the card, returning the set's name, or null with <see cref="ErrorText"/> saying what's missing. </summary>
    public string? TrySave()
    {
        ErrorText = "";

        if (SelectedDeck is not { } deck)
        {
            ErrorText = "Choose a set to add the card to.";
            return null;
        }

        FlashCard card;
        if (IsCloze)
        {
            if (ClozeUtility.Groups(ClozeText).Count == 0)
            {
                ErrorText = @"Mark at least one blank: select some text and press Blank (Ctrl+Shift+C), or type \C{...}.";
                return null;
            }
            card = new ClozeFlashCard(ClozeText.Trim(), Extra.Trim(), typeAnswer: false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Front) || string.IsNullOrWhiteSpace(Back))
            {
                ErrorText = "A Flip card needs both a front and a back.";
                return null;
            }
            card = new FlipFlashCard(Front.Trim(), Back.Trim());
        }

        FlashCardRepository.SaveNewCard(card, deck.ID);
        _lastCardType = SelectedCardType;
        _lastDeckID = deck.ID;
        return deck.Name;
    }
}
