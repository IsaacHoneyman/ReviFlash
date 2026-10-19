using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public partial class StudyGroup(string name) : LibraryItem(name)
{
    [NotifyPropertyChangedFor(nameof(CardSubtitle))]
    [ObservableProperty] private int _cardCount;
    [NotifyPropertyChangedFor(nameof(DeckCountLabel))]
    [ObservableProperty] private int _deckCount;

    public string DeckCountLabel => DeckCount == 1 ? "Set" : "Sets";

    /// <summary> Folder this group lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    protected override string CardSummary => $"{TextUtility.Plural(CardCount, "card")} · {StudiedText(LastStudied)}";

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

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => [CardCount.ToString(), $"{DeckCount} sets", FolderPath];
    public override int SortCardCount => CardCount;
}
