using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReviFlash.Models;

public partial class FlashCardDeck(string name) : LibraryItem(name)
{
    [ObservableProperty] private int _cardCount;

    [NotifyPropertyChangedFor(nameof(ShowLocationHint))]
    [NotifyPropertyChangedFor(nameof(ShowReviewHint))]
    [ObservableProperty] private bool _isSelectedForMultiReview;

    /// <summary> Folder this set lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    /// <summary> A search result found in another folder says where it lives instead. </summary>
    public bool ShowLocationHint => HasFolderPath && !IsSelectedForMultiReview;
    public bool ShowReviewHint => !HasFolderPath && !IsSelectedForMultiReview;

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

    protected override void OnFolderPathUpdated()
    {
        OnPropertyChanged(nameof(ShowLocationHint));
        OnPropertyChanged(nameof(ShowReviewHint));
    }

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => [CardCount.ToString(), FolderPath];
    public override int SortCardCount => CardCount;
}
