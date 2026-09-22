using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public partial class StudyGroup(string name) : ObservableObject, ISearchable
{
    public ulong ID { get; private set; } = ulong.MaxValue;

    [ObservableProperty] private string _name = name;
    [ObservableProperty] private int _cardCount;
    [ObservableProperty] private int _deckCount;
    [ObservableProperty] private bool _isSelectedForMultiReview;

    /// <summary> Folder this group lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    /// <summary> Breadcrumb text shown when a search surfaces this group from inside a folder. </summary>
    [NotifyPropertyChangedFor(nameof(HasFolderPath))]
    [NotifyPropertyChangedFor(nameof(ShowLocationHint))]
    [NotifyPropertyChangedFor(nameof(ShowReviewHint))]
    [ObservableProperty] private string? _folderPath;

    public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

    /// <summary> A search result found in another folder says where it lives instead. </summary>
    public bool ShowLocationHint => HasFolderPath;
    public bool ShowReviewHint => !HasFolderPath;

    /// <summary> Combined study time of the group's member decks. </summary>
    public int StudySeconds { get; set; }

    /// <summary> Most recent day any member deck was studied. </summary>
    public DateTime? LastStudied { get; set; }

    public StudyGroup(string name, ulong id, int deckCount, int cardCount) : this(name)
    {
        ID = id;
        DeckCount = deckCount;
        CardCount = cardCount;
    }

    public StudyGroup(string name, ulong id, int deckCount, int cardCount, ulong? folderID, int studySeconds, DateTime? lastStudied)
        : this(name, id, deckCount, cardCount)
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

    // --- Search ---

    public string SearchName => Name;
    public IEnumerable<string?> SearchKeywords => [CardCount.ToString(), $"{DeckCount} decks", FolderPath];
    public int SortCardCount => CardCount;
    public int SortStudySeconds => StudySeconds;
    public DateTime? SortLastActivity => LastStudied;
}
