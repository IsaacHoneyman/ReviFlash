using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> A page of lecture notes, filed in folders alongside sets and groups. Local only. </summary>
public partial class Note : LibraryItem
{
    /// <summary> Stable across devices, so notes can be synced later without changing their IDs. </summary>
    public string SyncID { get; }

    /// <summary> Folder this note lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    [NotifyPropertyChangedFor(nameof(Summary))]
    [ObservableProperty] private int _wordCount;

    [NotifyPropertyChangedFor(nameof(Summary))]
    [ObservableProperty] private DateTime _updatedAt;

    public DateTime CreatedAt { get; }

    /// <summary> What the note card says under the name, e.g. "1,204 words · edited 3 Oct". </summary>
    public string Summary => $"{(WordCount == 1 ? "1 word" : $"{WordCount:N0} words")} · edited {TextUtility.FormatWhen(UpdatedAt)}";

    public override string Kind => "note";
    public override ulong? ContainerFolderID => FolderID;

    public Note(ulong id, string syncID, string name, ulong? folderID, int wordCount, DateTime createdAt, DateTime updatedAt) : base(name)
    {
        ID = id;
        SyncID = syncID;
        _folderID = folderID;
        _wordCount = wordCount;
        CreatedAt = createdAt;
        _updatedAt = updatedAt;
    }

    // --- Search ---

    public override IEnumerable<string?> SearchKeywords => ["note", FolderPath];

    /// <summary> Falls back to the last edit until a study day is known. </summary>
    public override DateTime? SortLastActivity => LastStudied ?? UpdatedAt;
}
