using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public partial class FlashCardDeck(string name) : ObservableObject, ISearchable
{
    public ulong ID { get; private set; } = ulong.MaxValue;

    [ObservableProperty] private string _name = name;
    [ObservableProperty] private int _cardCount;

    [NotifyPropertyChangedFor(nameof(ShowLocationHint))]
    [NotifyPropertyChangedFor(nameof(ShowReviewHint))]
    [ObservableProperty] private bool _isSelectedForMultiReview;

    /// <summary> Folder this set lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    /// <summary> Breadcrumb text shown when a search surfaces this set from inside a folder. </summary>
    [NotifyPropertyChangedFor(nameof(HasFolderPath))]
    [NotifyPropertyChangedFor(nameof(ShowLocationHint))]
    [NotifyPropertyChangedFor(nameof(ShowReviewHint))]
    [ObservableProperty] private string? _folderPath;

    public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

    /// <summary> A search result found in another folder says where it lives instead. </summary>
    public bool ShowLocationHint => HasFolderPath && !IsSelectedForMultiReview;
    public bool ShowReviewHint => !HasFolderPath && !IsSelectedForMultiReview;

    /// <summary> Total recorded study time, populated on load so sorting needs no extra queries. </summary>
    public int StudySeconds { get; set; }

    /// <summary> Most recent day this set was studied, or null if it never has been. </summary>
    public DateTime? LastStudied { get; set; }

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

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
    }

    public void AddFlashCard(FlashCard card)
    {
        flashCards.Add(card);
    }

    public void RemoveFlashCard(FlashCard card)
    {
        flashCards.Remove(card);
    }

    // --- Search ---

    public string SearchName => Name;
    public IEnumerable<string?> SearchKeywords => [CardCount.ToString(), FolderPath];
    public int SortCardCount => CardCount;
    public int SortStudySeconds => StudySeconds;
    public DateTime? SortLastActivity => LastStudied;
}
