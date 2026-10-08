using System;
using System.Collections.Generic;
using System.Linq;
using ReviFlash.Data.Local;
using ReviFlash.Models;

namespace ReviFlash.Utilities;

/// <summary> In-memory folder hierarchy, built once per dashboard refresh and shared by navigation, breadcrumbs, search and the move dialog. </summary>
public sealed class FolderTree
{
    public const string PathSeparator = " / ";

    public const string RootLabel = "Main Menu";

    private readonly Dictionary<ulong, Folder> _byId;
    private readonly Dictionary<ulong, List<Folder>> _childrenByParent = [];
    private readonly List<Folder> _rootFolders = [];

    public IReadOnlyList<Folder> AllFolders { get; }

    public FolderTree(IEnumerable<Folder> folders)
    {
        AllFolders = [.. folders];
        _byId = AllFolders.ToDictionary(folder => folder.ID);

        foreach (var folder in AllFolders)
        {
            // A folder whose parent is missing is treated as a root folder so it stays reachable.
            if (folder.ParentFolderID is ulong parentId && _byId.ContainsKey(parentId))
            {
                if (!_childrenByParent.TryGetValue(parentId, out var siblings))
                {
                    siblings = [];
                    _childrenByParent[parentId] = siblings;
                }

                siblings.Add(folder);
            }
            else
            {
                _rootFolders.Add(folder);
            }
        }
    }

    public static FolderTree Load() => new(FolderRepository.GetAllFolders());

    public Folder? Get(ulong? folderID) =>
        folderID is ulong id && _byId.TryGetValue(id, out var folder) ? folder : null;

    public bool Exists(ulong? folderID) => folderID is null || _byId.ContainsKey(folderID.Value);

    public IReadOnlyList<Folder> ChildrenOf(ulong? parentFolderID)
    {
        if (parentFolderID is not ulong id) return _rootFolders;
        return _childrenByParent.TryGetValue(id, out var children) ? children : [];
    }

    /// <summary> The folder and its ancestors, outermost first. Empty for the main menu. </summary>
    public IReadOnlyList<Folder> AncestorChain(ulong? folderID)
    {
        var chain = new List<Folder>();
        var visited = new HashSet<ulong>();

        var current = Get(folderID);
        while (current is not null && visited.Add(current.ID))
        {
            chain.Add(current);
            current = Get(current.ParentFolderID);
        }

        chain.Reverse();
        return chain;
    }

    /// <summary> Breadcrumb text for a folder, e.g. "Biology / Cells". Empty at the main menu. </summary>
    public string PathOf(ulong? folderID) =>
        string.Join(PathSeparator, AncestorChain(folderID).Select(folder => folder.Name));

    /// <summary> A folder plus every folder beneath it. </summary>
    public HashSet<ulong> SubtreeIds(ulong folderID)
    {
        var ids = new HashSet<ulong>();
        var pending = new Stack<ulong>();
        pending.Push(folderID);

        while (pending.Count > 0)
        {
            ulong current = pending.Pop();
            if (!ids.Add(current)) continue;

            foreach (var child in ChildrenOf(current)) pending.Push(child.ID);
        }

        return ids;
    }

    /// <summary> A null root means the whole library. </summary>
    public bool IsInSubtree(ulong? folderID, ulong? rootFolderID)
    {
        if (rootFolderID is not ulong root) return true;
        if (folderID is not ulong id) return false;

        return SubtreeIds(root).Contains(id);
    }

    /// <summary> Card totals and study time roll up through the subtree; sets, groups, notes and subfolders count direct children only. </summary>
    public void ApplyCounts(IEnumerable<FlashCardDeck> decks, IEnumerable<StudyGroup> groups, IEnumerable<Note>? notes = null)
    {
        var deckList = decks as IReadOnlyList<FlashCardDeck> ?? [.. decks];
        var groupList = groups as IReadOnlyList<StudyGroup> ?? [.. groups];

        foreach (var folder in AllFolders)
        {
            folder.SubFolderCount = ChildrenOf(folder.ID).Count;
            folder.SetCount = 0;
            folder.GroupCount = 0;
            folder.NoteCount = 0;
            folder.CardCount = 0;
            folder.StudySeconds = 0;
            folder.LastStudied = null;
        }

        foreach (var deck in deckList)
        {
            if (Get(deck.FolderID) is Folder folder) folder.SetCount++;
        }

        foreach (var group in groupList)
        {
            if (Get(group.FolderID) is Folder folder) folder.GroupCount++;
        }

        foreach (var note in notes ?? [])
        {
            if (Get(note.FolderID) is Folder folder) folder.NoteCount++;
        }

        // Groups are skipped: their cards live in decks already counted.
        foreach (var folder in AllFolders)
        {
            var subtree = SubtreeIds(folder.ID);

            foreach (var deck in deckList)
            {
                if (deck.FolderID is not ulong deckFolderId || !subtree.Contains(deckFolderId)) continue;

                folder.CardCount += deck.CardCount;
                folder.StudySeconds += deck.StudySeconds;

                if (deck.LastStudied is DateTime studied &&
                    (folder.LastStudied is null || studied > folder.LastStudied))
                {
                    folder.LastStudied = studied;
                }
            }
        }
    }

    /// <summary> Stamps each item with its folder path relative to <paramref name="relativeTo"/>, so deeper search results show where they came from. </summary>
    public void ApplyPaths(IEnumerable<FlashCardDeck> decks, IEnumerable<StudyGroup> groups, ulong? relativeTo = null, IEnumerable<Note>? notes = null)
    {
        string basePath = PathOf(relativeTo);

        foreach (var deck in decks) deck.FolderPath = RelativePath(deck.FolderID, relativeTo, basePath);
        foreach (var group in groups) group.FolderPath = RelativePath(group.FolderID, relativeTo, basePath);
        foreach (var note in notes ?? []) note.FolderPath = RelativePath(note.FolderID, relativeTo, basePath);

        foreach (var folder in AllFolders)
        {
            folder.FolderPath = RelativePath(folder.ParentFolderID, relativeTo, basePath);
        }
    }

    private string RelativePath(ulong? folderID, ulong? relativeTo, string basePath)
    {
        if (folderID == relativeTo) return string.Empty;

        string full = PathOf(folderID);
        if (string.IsNullOrEmpty(full)) return RootLabel;

        // Trim the part of the path the user is already standing in.
        if (basePath.Length > 0 && full.StartsWith(basePath, StringComparison.Ordinal))
        {
            string trimmed = full[basePath.Length..];
            if (trimmed.StartsWith(PathSeparator, StringComparison.Ordinal))
            {
                trimmed = trimmed[PathSeparator.Length..];
            }

            return trimmed;
        }

        return full;
    }
}
