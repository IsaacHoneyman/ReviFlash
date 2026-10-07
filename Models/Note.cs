using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> A page of lecture notes, filed in folders alongside sets and groups. Local only. </summary>
public partial class Note : ObservableObject, ISearchable
{
    public ulong ID { get; }

    /// <summary> Stable across devices, so notes can be synced later without changing their IDs. </summary>
    public string SyncID { get; }

    [ObservableProperty] private string _name;

    /// <summary> Folder this note lives in, or null when it sits at the main menu. </summary>
    [ObservableProperty] private ulong? _folderID;

    /// <summary> Breadcrumb text shown when a search surfaces this note from inside a folder. </summary>
    [NotifyPropertyChangedFor(nameof(HasFolderPath))]
    [ObservableProperty] private string? _folderPath;

    public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

    [NotifyPropertyChangedFor(nameof(Summary))]
    [ObservableProperty] private int _wordCount;

    [NotifyPropertyChangedFor(nameof(Summary))]
    [ObservableProperty] private DateTime _updatedAt;

    public DateTime CreatedAt { get; }

    /// <summary> All the time spent in the note, for the study time sorts. </summary>
    public int StudySeconds { get; init; }

    /// <summary> What the note card says under the name, e.g. "1,204 words · edited 3 Oct". </summary>
    public string Summary => $"{(WordCount == 1 ? "1 word" : $"{WordCount:N0} words")} · edited {TextUtility.FormatWhen(UpdatedAt)}";

    public Note(ulong id, string syncID, string name, ulong? folderID, int wordCount, DateTime createdAt, DateTime updatedAt)
    {
        ID = id;
        SyncID = syncID;
        _name = name;
        _folderID = folderID;
        _wordCount = wordCount;
        CreatedAt = createdAt;
        _updatedAt = updatedAt;
    }

    // --- Search ---

    public string SearchName => Name;
    public IEnumerable<string?> SearchKeywords => ["note", FolderPath];
    public int SortStudySeconds => StudySeconds;
    public DateTime? SortLastActivity => UpdatedAt;
}
