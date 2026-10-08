using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

/// <summary> Folders only record where items are filed; only <see cref="DeleteFolderAndContents"/> touches what's inside them. </summary>
public static class FolderRepository
{
    // --- Reads ---

    /// <summary> Every folder, flat; callers derive counts from the sets and groups they hold, so they match the screen. </summary>
    public static List<Folder> GetAllFolders()
    {
        using var connection = Db.Open();
        return GetAllFolders(connection, null);
    }

    private static List<Folder> GetAllFolders(SqliteConnection connection, SqliteTransaction? transaction) =>
        Db.Query(connection, transaction, "SELECT ID, Name, ParentFolderID FROM Folders ORDER BY Name COLLATE NOCASE;",
            reader => new Folder(reader.GetString(1), (ulong)reader.GetInt64(0), reader.IsDBNull(2) ? null : (ulong)reader.GetInt64(2)));

    // --- Writes ---

    public static Folder CreateFolder(string name, ulong? parentFolderID)
    {
        using var connection = Db.Open();
        return CreateFolder(connection, null, name, parentFolderID);
    }

    private static Folder CreateFolder(SqliteConnection connection, SqliteTransaction? transaction, string name, ulong? parentFolderID)
    {
        var folder = new Folder(name);
        folder.AssignDatabaseID(Db.Insert(connection, transaction, "INSERT INTO Folders (Name, ParentFolderID) VALUES ($name, $parentId);",
            ("$name", name), ("$parentId", parentFolderID)));
        folder.ParentFolderID = parentFolderID;
        return folder;
    }

    /// <summary> Walks down from <paramref name="parentFolderID"/>, reusing same-named folders and creating the rest. </summary>
    internal static ulong? EnsureFolderPath(SqliteConnection connection, SqliteTransaction transaction, ulong? parentFolderID, IEnumerable<string> folderNames)
    {
        FolderTree? tree = null;
        bool creating = false;

        foreach (var name in folderNames)
        {
            var existing = creating ? null : (tree ??= new FolderTree(GetAllFolders(connection, transaction))).ChildrenOf(parentFolderID)
                .FirstOrDefault(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing is null) creating = true;
            parentFolderID = existing?.ID ?? CreateFolder(connection, transaction, name, parentFolderID).ID;
        }

        return parentFolderID;
    }

    public static void RenameFolder(ulong folderID, string name) =>
        Db.Execute("UPDATE Folders SET Name = $name WHERE ID = $id;", ("$name", name), ("$id", folderID));

    /// <summary> Removes a folder and lifts everything inside it up to its parent; nothing else is deleted. </summary>
    public static void DeleteFolder(ulong folderID)
    {
        Db.InTransaction((connection, transaction) =>
        {
            // Resolved up front: after the DELETE the parent link is gone.
            ulong? parentID = GetParentFolderID(connection, transaction, folderID);

            UpdateFolderColumn(connection, transaction, "Folders", "ParentFolderID", "ParentFolderID", folderID, parentID);
            UpdateFolderColumn(connection, transaction, "Decks", "FolderID", "FolderID", folderID, parentID);
            UpdateFolderColumn(connection, transaction, "StudyGroups", "FolderID", "FolderID", folderID, parentID);
            UpdateFolderColumn(connection, transaction, "Notes", "FolderID", "FolderID", folderID, parentID);

            Db.Execute(connection, transaction, "DELETE FROM Folders WHERE ID = $id;", ("$id", folderID));
        });
    }

    /// <summary> How much <see cref="DeleteFolderAndContents"/> would delete, for the confirmation. </summary>
    public static (int Folders, int Sets, int Groups, int Notes, int Cards) CountContents(ICollection<ulong> subtreeIDs)
    {
        var ids = IdList(subtreeIDs);
        var (sets, groups, notes, cards) = Db.Query($@"
            SELECT
                (SELECT COUNT(*) FROM Decks WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM StudyGroups WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM Notes WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM Cards WHERE DeckID IN (SELECT ID FROM Decks WHERE FolderID IN ({ids})));",
            reader => (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)))[0];
        return (subtreeIDs.Count - 1, sets, groups, notes, cards);
    }

    /// <summary> Deletes a folder and its descendants (the subtree IDs) with all their groups, notes, sets, cards and stats. </summary>
    public static void DeleteFolderAndContents(ICollection<ulong> subtreeIDs)
    {
        var ids = IdList(subtreeIDs);
        Db.InTransaction((connection, transaction) => Db.Execute(connection, transaction, $@"
            DELETE FROM AnswerStreaks
            WHERE (TargetType = $deckTarget AND TargetId IN (SELECT ID FROM Decks WHERE FolderID IN ({ids})))
               OR (TargetType = $groupTarget AND TargetId IN (SELECT ID FROM StudyGroups WHERE FolderID IN ({ids})));
            DELETE FROM Decks WHERE FolderID IN ({ids});
            DELETE FROM StudyGroups WHERE FolderID IN ({ids});
            DELETE FROM NoteStats WHERE NoteID IN (SELECT ID FROM Notes WHERE FolderID IN ({ids}));
            DELETE FROM Notes WHERE FolderID IN ({ids});
            DELETE FROM Folders WHERE ID IN ({ids});",
            ("$deckTarget", FlashCardRepository.DeckStreakTarget), ("$groupTarget", FlashCardRepository.GroupStreakTarget)));
    }

    /// <summary> IDs for an IN (...) list; they're numbers, so they can go in the SQL directly. </summary>
    private static string IdList(IEnumerable<ulong> ids) =>
        string.Join(", ", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    /// <summary> Refiles a folder. Rejects moves that would make a folder its own ancestor. </summary>
    public static void MoveFolder(ulong folderID, ulong? newParentFolderID)
    {
        if (newParentFolderID == folderID)
        {
            throw new InvalidOperationException("A folder cannot be moved into itself.");
        }

        if (newParentFolderID.HasValue && IsDescendantOf(newParentFolderID.Value, folderID))
        {
            throw new InvalidOperationException("A folder cannot be moved into one of its own subfolders.");
        }

        MoveItem("Folders", "ParentFolderID", folderID, newParentFolderID);
    }

    public static void MoveDeck(ulong deckID, ulong? folderID) => MoveItem("Decks", "FolderID", deckID, folderID);

    public static void MoveStudyGroup(ulong groupID, ulong? folderID) => MoveItem("StudyGroups", "FolderID", groupID, folderID);

    public static void MoveNote(ulong noteID, ulong? folderID) => MoveItem("Notes", "FolderID", noteID, folderID);

    /// <summary> True when <paramref name="candidateID"/> sits anywhere below <paramref name="ancestorID"/>. </summary>
    public static bool IsDescendantOf(ulong candidateID, ulong ancestorID)
    {
        return Db.Scalar<long?>(@"
            WITH RECURSIVE Ancestors(ID) AS (
                SELECT ParentFolderID FROM Folders WHERE ID = $candidateId
                UNION
                SELECT f.ParentFolderID FROM Folders f
                INNER JOIN Ancestors a ON f.ID = a.ID
                WHERE f.ParentFolderID IS NOT NULL
            )
            SELECT 1 FROM Ancestors WHERE ID = $ancestorId LIMIT 1;",
            ("$candidateId", candidateID), ("$ancestorId", ancestorID)) is not null;
    }

    // --- Helpers ---

    private static ulong? GetParentFolderID(SqliteConnection connection, SqliteTransaction transaction, ulong folderID) =>
        (ulong?)Db.Scalar<long?>(connection, transaction, "SELECT ParentFolderID FROM Folders WHERE ID = $id;", ("$id", folderID));

    private static void MoveItem(string table, string folderColumn, ulong id, ulong? folderID)
    {
        using var connection = Db.Open();
        UpdateFolderColumn(connection, null, table, folderColumn, "ID", id, folderID);
    }

    /// <summary> Files every row of <paramref name="table"/> whose <paramref name="matchColumn"/> is <paramref name="match"/> into <paramref name="folderID"/>. </summary>
    private static void UpdateFolderColumn(SqliteConnection connection, SqliteTransaction? transaction,
        string table, string folderColumn, string matchColumn, ulong match, ulong? folderID) =>
        Db.Execute(connection, transaction, $"UPDATE {table} SET {folderColumn} = $folderId WHERE {matchColumn} = $match;",
            ("$folderId", folderID), ("$match", match));
}
