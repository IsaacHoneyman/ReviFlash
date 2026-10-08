using System;
using System.Collections.Generic;
using System.Globalization;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

/// <summary> Note storage. Times are kept as UTC round-trip strings and handed out as local time. </summary>
public static class NoteRepository
{
    // --- Reads ---

    /// <summary> Every note without its content (the word count comes along instead), for the main menu. </summary>
    public static List<Note> GetAllNotes()
    {
        var notes = new List<Note>();

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT n.ID, n.SyncID, n.Name, n.FolderID, n.Content, n.CreatedAt, n.UpdatedAt, COALESCE(s.Seconds, 0)
            FROM Notes n
            LEFT JOIN (SELECT NoteID, SUM(Seconds) AS Seconds FROM NoteStats GROUP BY NoteID) s ON s.NoteID = n.ID
            ORDER BY n.Name COLLATE NOCASE;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            notes.Add(new Note(
                (ulong)reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : (ulong)reader.GetInt64(3),
                NoteText.WordCount(reader.GetString(4)),
                ReadTime(reader.GetString(5)),
                ReadTime(reader.GetString(6)))
            {
                StudySeconds = reader.GetInt32(7),
            });
        }

        return notes;
    }

    // --- Study time ---

    /// <summary> Adds time spent in a note to today's total. </summary>
    public static void AddStudyTime(ulong noteID, int seconds)
    {
        if (seconds <= 0) return;

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO NoteStats (NoteID, Seconds) VALUES ($id, $seconds)
            ON CONFLICT(NoteID, DateStudied) DO UPDATE SET Seconds = Seconds + excluded.Seconds;";
        command.Parameters.AddWithValue("$id", noteID);
        command.Parameters.AddWithValue("$seconds", seconds);
        command.ExecuteNonQuery();
    }

    /// <summary> Seconds spent in one note, or all notes when null, within the time period (an SQLite date modifier). </summary>
    public static int GetStudySeconds(ulong? noteID, string? timeModifier)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(Seconds), 0) FROM NoteStats WHERE 1=1" + StatsFilter(command, noteID, timeModifier) + ";";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary> Seconds per day, oldest first, for one note or all notes. </summary>
    public static List<(DateOnly date, int seconds)> GetStudyTimeByDate(ulong? noteID, string? timeModifier)
    {
        var rows = new List<(DateOnly, int)>();

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DateStudied, SUM(Seconds) FROM NoteStats WHERE 1=1"
            + StatsFilter(command, noteID, timeModifier) + " GROUP BY DateStudied ORDER BY DateStudied;";

        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add((DateOnly.Parse(reader.GetString(0), CultureInfo.InvariantCulture), reader.GetInt32(1)));
        return rows;
    }

    public static List<(ulong noteID, int seconds)> GetStudyTimeByNote(string? timeModifier)
    {
        var rows = new List<(ulong, int)>();

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteID, SUM(Seconds) FROM NoteStats WHERE 1=1"
            + StatsFilter(command, null, timeModifier) + " GROUP BY NoteID;";

        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add(((ulong)reader.GetInt64(0), reader.GetInt32(1)));
        return rows;
    }

    public static DateOnly? GetLastStudied(ulong noteID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(DateStudied) FROM NoteStats WHERE NoteID = $id;";
        command.Parameters.AddWithValue("$id", noteID);

        return command.ExecuteScalar() is string raw ? DateOnly.Parse(raw, CultureInfo.InvariantCulture) : null;
    }

    /// <summary> The WHERE conditions (and their parameters) for a note and a time period, either optional. </summary>
    private static string StatsFilter(Microsoft.Data.Sqlite.SqliteCommand command, ulong? noteID, string? timeModifier)
    {
        var sql = "";
        if (noteID.HasValue)
        {
            sql += " AND NoteID = $noteId";
            command.Parameters.AddWithValue("$noteId", noteID.Value);
        }
        if (!string.IsNullOrEmpty(timeModifier))
        {
            sql += " AND DateStudied >= DATE('now', $timeModifier)";
            command.Parameters.AddWithValue("$timeModifier", timeModifier);
        }
        return sql;
    }

    public static string GetContent(ulong noteID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Content FROM Notes WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", noteID);

        return command.ExecuteScalar() as string ?? "";
    }

    // --- Writes ---

    public static Note CreateNote(string name, ulong? folderID)
    {
        var syncID = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Notes (SyncID, Name, FolderID, Content, CreatedAt, UpdatedAt)
            VALUES ($syncId, $name, $folderId, '', $now, $now);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$syncId", syncID);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$folderId", folderID.HasValue ? folderID.Value : DBNull.Value);
        command.Parameters.AddWithValue("$now", WriteTime(now));

        var id = (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert note."));
        return new Note((ulong)id, syncID, name, folderID, 0, now.ToLocalTime(), now.ToLocalTime());
    }

    /// <summary> Saves the content and returns the new edited time (local). </summary>
    public static DateTime SaveContent(ulong noteID, string content)
    {
        var now = DateTime.UtcNow;

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Notes SET Content = $content, UpdatedAt = $now WHERE ID = $id;";
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$now", WriteTime(now));
        command.Parameters.AddWithValue("$id", noteID);
        command.ExecuteNonQuery();

        return now.ToLocalTime();
    }

    public static void RenameNote(ulong noteID, string name)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Notes SET Name = $name WHERE ID = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", noteID);
        command.ExecuteNonQuery();
    }

    public static void DeleteNote(ulong noteID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NoteStats WHERE NoteID = $id; DELETE FROM Notes WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", noteID);
        command.ExecuteNonQuery();
    }

    // --- Helpers ---

    private static string WriteTime(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ReadTime(string raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime()
            : DateTime.Now;
}
