using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public partial class FlashCardDeck(string name) : LibraryItem(name)
{
    [NotifyPropertyChangedFor(nameof(CardCountText), nameof(CardCountLabel))]
    [ObservableProperty] private int _cardCount;

    public string CardCountText => TextUtility.Plural(CardCount, "card");
    public string CardCountLabel => CardCount == 1 ? "Card" : "Cards";

    [ObservableProperty] private bool _isSelectedForMultiReview;

    /// <summary> Folder this set lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;


    public override string Kind => "set";
    public override ulong? ContainerFolderID => FolderID;

    private readonly List<FlashCard> flashCards = [];
    public IReadOnlyList<FlashCard> FlashCards => flashCards.AsReadOnly();

    public FlashCardDeck(string name, ulong id, int cardCount) : this(name)
    {
        ID = id;
        CardCount = cardCount;
    }

    public FlashCardDeck(string name, ulong id, int cardCount, ulong? folderID, int studySeconds, DateTime? lastStudied)
        : this(name, id, cardCount)
    {
        FolderID = folderID;
        StudySeconds = studySeconds;
        LastStudied = lastStudied;
    }

    public void AddFlashCard(FlashCard card)
    {
        flashCards.Add(card);
    }

    public void RemoveFlashCard(FlashCard card)
    {
        flashCards.Remove(card);
    }

    protected override string CardSummary => TextUtility.Capitalise(StudiedText(LastStudied));

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => [CardCount.ToString(), FolderPath];
    public override int SortCardCount => CardCount;
}
