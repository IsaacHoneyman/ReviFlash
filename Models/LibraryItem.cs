using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> Anything the library lists and files in folders: sets, study groups, notes and folders. </summary>
public abstract partial class LibraryItem : ObservableObject, ISearchable
{
    public ulong ID { get; protected set; } = ulong.MaxValue;

    [ObservableProperty] private string _name;

    /// <summary> Breadcrumb text shown when a search surfaces this item from inside a folder. </summary>
    [NotifyPropertyChangedFor(nameof(HasFolderPath))]
    [ObservableProperty] private string? _folderPath;

    public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

    /// <summary> Total recorded study time, populated on load so sorting needs no extra queries. </summary>
    public int StudySeconds { get; set; }

    /// <summary> Most recent day this was studied, or null if it never has been. </summary>
    public DateTime? LastStudied { get; set; }

    /// <summary> Lower-case noun for messages, e.g. "set" or "folder". </summary>
    public abstract string Kind { get; }

    /// <summary> The folder this item sits in, or null at the main menu. </summary>
    public abstract ulong? ContainerFolderID { get; }

    protected LibraryItem(string name)
    {
        _name = name;
    }

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
    }

    partial void OnFolderPathChanged(string? value) => OnFolderPathUpdated();

    /// <summary> Lets an item refresh properties that depend on <see cref="FolderPath"/>. </summary>
    protected virtual void OnFolderPathUpdated() { }

    // --- Search ---

    public string SearchName => Name;
    public abstract IEnumerable<string?> SearchKeywords { get; }
    public virtual int SortCardCount => 0;
    public int SortStudySeconds => StudySeconds;
    public virtual DateTime? SortLastActivity => LastStudied;
}
