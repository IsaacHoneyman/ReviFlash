using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> Holds sets, notes, groups and other folders; purely organisational, never affects review. </summary>
public partial class Folder(string name) : LibraryItem(name)
{
    /// <summary> Parent folder, or null when the folder sits at the main menu. </summary>
    [ObservableProperty] private ulong? _parentFolderID;

    /// <summary> Direct child counts, shown on the folder card. </summary>
    [NotifyPropertyChangedFor(nameof(ItemCount), nameof(ItemCountLabel))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary), nameof(CardSubtitle))]
    [ObservableProperty] private int _subFolderCount;
    [NotifyPropertyChangedFor(nameof(ItemCount), nameof(ItemCountLabel))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary), nameof(CardSubtitle))]
    [ObservableProperty] private int _setCount;
    [NotifyPropertyChangedFor(nameof(ItemCount), nameof(ItemCountLabel))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary), nameof(CardSubtitle))]
    [ObservableProperty] private int _groupCount;
    [NotifyPropertyChangedFor(nameof(ItemCount), nameof(ItemCountLabel))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary), nameof(CardSubtitle))]
    [ObservableProperty] private int _noteCount;

    /// <summary> Cards across the whole subtree, so a folder of folders still reads usefully. </summary>
    [ObservableProperty] private int _cardCount;

    public int ItemCount => SubFolderCount + SetCount + GroupCount + NoteCount;
    public string ItemCountLabel => ItemCount == 1 ? "Item" : "Items";

    public string ContentsSummary
    {
        get
        {
            List<string> parts = [];
            if (SubFolderCount > 0) parts.Add(TextUtility.Plural(SubFolderCount, "folder"));
            if (SetCount > 0) parts.Add(TextUtility.Plural(SetCount, "set"));
            if (GroupCount > 0) parts.Add(TextUtility.Plural(GroupCount, "group"));
            if (NoteCount > 0) parts.Add(TextUtility.Plural(NoteCount, "note"));

            return parts.Count == 0 ? "Empty folder" : string.Join(" · ", parts);
        }
    }

    public override string Kind => "folder";
    public override ulong? ContainerFolderID => ParentFolderID;

    public Folder(string name, ulong id, ulong? parentFolderID) : this(name)
    {
        ID = id;
        ParentFolderID = parentFolderID;
    }

    protected override string CardSummary => ContentsSummary;

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => [ContentsSummary, FolderPath];
    public override int SortCardCount => CardCount;
}
