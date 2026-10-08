using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> Holds sets, notes, groups and other folders; purely organisational, never affects review. </summary>
public partial class Folder(string name) : ObservableObject, ISearchable
{
    public ulong ID { get; private set; } = ulong.MaxValue;

    [ObservableProperty] private string _name = name;

    /// <summary> Parent folder, or null when the folder sits at the main menu. </summary>
    [ObservableProperty] private ulong? _parentFolderID;

    /// <summary> Direct child counts, shown on the folder card. </summary>
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary))]
    [ObservableProperty] private int _subFolderCount;
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary))]
    [ObservableProperty] private int _setCount;
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary))]
    [ObservableProperty] private int _groupCount;
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    [NotifyPropertyChangedFor(nameof(ContentsSummary))]
    [ObservableProperty] private int _noteCount;

    /// <summary> Cards across the whole subtree, so a folder of folders still reads usefully. </summary>
    [ObservableProperty] private int _cardCount;

    /// <summary> Breadcrumb text shown when a search surfaces this folder from deeper down. </summary>
    [NotifyPropertyChangedFor(nameof(HasFolderPath))]
    [NotifyPropertyChangedFor(nameof(LocationSuffix))]
    [ObservableProperty] private string? _folderPath;

    public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

    /// <summary> Trails the contents summary on the card, so the location never needs a line of its own. </summary>
    public string LocationSuffix => HasFolderPath ? $" · in {FolderPath}" : "";

    /// <summary> Study time across the whole subtree. </summary>
    public int StudySeconds { get; set; }

    /// <summary> Most recent day anything in the subtree was studied. </summary>
    public DateTime? LastStudied { get; set; }

    public int ItemCount => SubFolderCount + SetCount + GroupCount + NoteCount;

    public string ContentsSummary
    {
        get
        {
            List<string> parts = [];
            if (SubFolderCount > 0) parts.Add(SubFolderCount == 1 ? "1 folder" : $"{SubFolderCount} folders");
            if (SetCount > 0) parts.Add(SetCount == 1 ? "1 set" : $"{SetCount} sets");
            if (GroupCount > 0) parts.Add(GroupCount == 1 ? "1 group" : $"{GroupCount} groups");
            if (NoteCount > 0) parts.Add(NoteCount == 1 ? "1 note" : $"{NoteCount} notes");

            return parts.Count == 0 ? "Empty folder" : string.Join(" · ", parts);
        }
    }

    public Folder(string name, ulong id, ulong? parentFolderID) : this(name)
    {
        ID = id;
        ParentFolderID = parentFolderID;
    }

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
    }

    // --- Search ---

    public string SearchName => Name;
    public IEnumerable<string?> SearchKeywords => [ContentsSummary, FolderPath];
    public int SortCardCount => CardCount;
    public int SortStudySeconds => StudySeconds;
    public DateTime? SortLastActivity => LastStudied;
}
