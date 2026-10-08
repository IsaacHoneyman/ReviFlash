using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReviFlash.Models;

public partial class StudyGroup(string name) : LibraryItem(name)
{
    [ObservableProperty] private int _cardCount;
    [ObservableProperty] private int _deckCount;

    /// <summary> Folder this group lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    /// <summary> A search result found in another folder says where it lives instead. </summary>
    public bool ShowLocationHint => HasFolderPath;
    public bool ShowReviewHint => !HasFolderPath;

    public override string Kind => "group";
    public override ulong? ContainerFolderID => FolderID;

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

    protected override void OnFolderPathUpdated()
    {
        OnPropertyChanged(nameof(ShowLocationHint));
        OnPropertyChanged(nameof(ShowReviewHint));
    }

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => [CardCount.ToString(), $"{DeckCount} decks", FolderPath];
    public override int SortCardCount => CardCount;
}
