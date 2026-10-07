using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

/// <summary>
/// Folder storage. Folders only ever record where a set, note or group is filed; deleting or
/// moving one never touches the cards, notes, stats or group memberships underneath it, unless
/// <see cref="DeleteFolderAndContents"/> is asked to take them too.
/// </summary>
public static class FolderRepository
{
    // --- Reads ---

    /// <summary>
    /// Every folder, flat, parent links included. Counts are derived by the caller from
    /// the sets and groups it already holds, so they always agree with what is on screen.
    /// </summary>
    public static List<Folder> GetAllFolders()
    {
        var folders = new List<Folder>();

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ID, Name, ParentFolderID
            FROM Folders
            ORDER BY Name COLLATE NOCASE;
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string name = reader.GetString(1);
            ulong? parentId = reader.IsDBNull(2) ? null : (ulong)reader.GetInt64(2);

            folders.Add(new Folder(name, id, parentId));
        }

        return folders;
    }

    public static bool FolderExists(ulong folderID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM Folders WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", folderID);

        return command.ExecuteScalar() is not null;
    }

    // --- Writes ---

    public static Folder CreateFolder(string name, ulong? parentFolderID)
    {
        var folder = new Folder(name);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Folders (Name, ParentFolderID) VALUES ($name, $parentId);
            SELECT last_insert_rowid();
        ";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$parentId", ToDbValue(parentFolderID));

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        folder.AssignDatabaseID((ulong)newID);
        folder.ParentFolderID = parentFolderID;

        return folder;
    }

    public static void RenameFolder(ulong folderID, string name)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Folders SET Name = $name WHERE ID = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", folderID);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes a folder and lifts everything inside it (subfolders, sets, notes and groups) up to
    /// the deleted folder's own parent. Nothing is ever deleted along with the folder.
    /// </summary>
    public static void DeleteFolder(ulong folderID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Resolved up front: after the DELETE the parent link is gone.
        ulong? parentID = GetParentFolderID(connection, transaction, folderID);

        PromoteChildren(connection, transaction, "Folders", "ParentFolderID", folderID, parentID);
        PromoteChildren(connection, transaction, "Decks", "FolderID", folderID, parentID);
        PromoteChildren(connection, transaction, "StudyGroups", "FolderID", folderID, parentID);
        PromoteChildren(connection, transaction, "Notes", "FolderID", folderID, parentID);

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM Folders WHERE ID = $id;";
        deleteCommand.Parameters.AddWithValue("$id", folderID);
        deleteCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    /// <summary> How much <see cref="DeleteFolderAndContents"/> would delete, for the confirmation. </summary>
    public static (int Folders, int Sets, int Groups, int Notes, int Cards) CountContents(ICollection<ulong> subtreeIDs)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        var ids = IdList(subtreeIDs);
        command.CommandText = $@"
            SELECT
                (SELECT COUNT(*) FROM Decks WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM StudyGroups WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM Notes WHERE FolderID IN ({ids})),
                (SELECT COUNT(*) FROM Cards WHERE DeckID IN (SELECT ID FROM Decks WHERE FolderID IN ({ids})));";
        using var reader = command.ExecuteReader();
        reader.Read();
        return (subtreeIDs.Count - 1, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    /// <summary>
    /// Deletes a folder with everything beneath it: subfolders, groups, notes, and sets with their cards and stats
    /// (which go with their set). <paramref name="subtreeIDs"/> is the folder and all its descendants.
    /// </summary>
    public static void DeleteFolderAndContents(ICollection<ulong> subtreeIDs)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var ids = IdList(subtreeIDs);
        command.CommandText = $@"
            DELETE FROM Decks WHERE FolderID IN ({ids});
            DELETE FROM StudyGroups WHERE FolderID IN ({ids});
            DELETE FROM NoteStats WHERE NoteID IN (SELECT ID FROM Notes WHERE FolderID IN ({ids}));
            DELETE FROM Notes WHERE FolderID IN ({ids});
            DELETE FROM Folders WHERE ID IN ({ids});";
        command.ExecuteNonQuery();
        transaction.Commit();
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

        UpdateFolderColumn("Folders", "ID", "ParentFolderID", folderID, newParentFolderID);
    }

    public static void MoveDeck(ulong deckID, ulong? folderID) =>
        UpdateFolderColumn("Decks", "ID", "FolderID", deckID, folderID);

    public static void MoveStudyGroup(ulong groupID, ulong? folderID) =>
        UpdateFolderColumn("StudyGroups", "ID", "FolderID", groupID, folderID);

    public static void MoveNote(ulong noteID, ulong? folderID) =>
        UpdateFolderColumn("Notes", "ID", "FolderID", noteID, folderID);

    /// <summary> True when <paramref name="candidateID"/> sits anywhere below <paramref name="ancestorID"/>. </summary>
    public static bool IsDescendantOf(ulong candidateID, ulong ancestorID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            WITH RECURSIVE Ancestors(ID) AS (
                SELECT ParentFolderID FROM Folders WHERE ID = $candidateId
                UNION ALL
                SELECT f.ParentFolderID FROM Folders f
                INNER JOIN Ancestors a ON f.ID = a.ID
                WHERE f.ParentFolderID IS NOT NULL
            )
            SELECT 1 FROM Ancestors WHERE ID = $ancestorId LIMIT 1;
        ";
        command.Parameters.AddWithValue("$candidateId", candidateID);
        command.Parameters.AddWithValue("$ancestorId", ancestorID);

        return command.ExecuteScalar() is not null;
    }

    // --- Helpers ---

    private static ulong? GetParentFolderID(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        ulong folderID)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ParentFolderID FROM Folders WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", folderID);

        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : (ulong)Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static void PromoteChildren(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string table,
        string folderColumn,
        ulong folderID,
        ulong? newFolderID)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {table} SET {folderColumn} = $newFolderId WHERE {folderColumn} = $folderId;";
        command.Parameters.AddWithValue("$newFolderId", ToDbValue(newFolderID));
        command.Parameters.AddWithValue("$folderId", folderID);
        command.ExecuteNonQuery();
    }

    private static void UpdateFolderColumn(string table, string keyColumn, string folderColumn, ulong id, ulong? folderID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {table} SET {folderColumn} = $folderId WHERE {keyColumn} = $id;";
        command.Parameters.AddWithValue("$folderId", ToDbValue(folderID));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static object ToDbValue(ulong? folderID) => folderID.HasValue ? folderID.Value : DBNull.Value;
}
