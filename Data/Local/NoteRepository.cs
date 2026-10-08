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
        return Db.Query(@"
            SELECT n.ID, n.SyncID, n.Name, n.FolderID, n.Content, n.CreatedAt, n.UpdatedAt, COALESCE(s.Seconds, 0)
            FROM Notes n
            LEFT JOIN (SELECT NoteID, SUM(Seconds) AS Seconds FROM NoteStats GROUP BY NoteID) s ON s.NoteID = n.ID
            ORDER BY n.Name COLLATE NOCASE;",
            reader => new Note(
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

    // --- Study time ---

    /// <summary> Adds time spent in a note to today's total. </summary>
    public static void AddStudyTime(ulong noteID, int seconds)
    {
        if (seconds <= 0) return;

        Db.Execute(@"
            INSERT INTO NoteStats (NoteID, Seconds) VALUES ($id, $seconds)
            ON CONFLICT(NoteID, DateStudied) DO UPDATE SET Seconds = Seconds + excluded.Seconds;",
            ("$id", noteID), ("$seconds", seconds));
    }

    /// <summary> Seconds spent in one note, or all notes when null, within the time period (an SQLite date modifier). </summary>
    public static int GetStudySeconds(ulong? noteID, string? timeModifier)
    {
        var filter = StatsFilter(noteID, timeModifier);
        return Db.Scalar<int>("SELECT COALESCE(SUM(Seconds), 0) FROM NoteStats WHERE 1=1" + filter.Sql + ";", filter.Parameters);
    }

    /// <summary> Seconds per day, oldest first, for one note or all notes. </summary>
    public static List<(DateOnly date, int seconds)> GetStudyTimeByDate(ulong? noteID, string? timeModifier)
    {
        var filter = StatsFilter(noteID, timeModifier);
        return Db.Query("SELECT DateStudied, SUM(Seconds) FROM NoteStats WHERE 1=1" + filter.Sql + " GROUP BY DateStudied ORDER BY DateStudied;",
            reader => (Db.ParseDay(reader.GetString(0)), reader.GetInt32(1)),
            filter.Parameters);
    }

    public static List<(ulong noteID, int seconds)> GetStudyTimeByNote(string? timeModifier)
    {
        var filter = StatsFilter(null, timeModifier);
        return Db.Query("SELECT NoteID, SUM(Seconds) FROM NoteStats WHERE 1=1" + filter.Sql + " GROUP BY NoteID;",
            reader => ((ulong)reader.GetInt64(0), reader.GetInt32(1)),
            filter.Parameters);
    }

    public static DateOnly? GetLastStudied(ulong noteID) =>
        Db.Scalar<string>("SELECT MAX(DateStudied) FROM NoteStats WHERE NoteID = $id;", ("$id", noteID)) is { } raw ? Db.ParseDay(raw) : null;

    private static (string Sql, (string Name, object? Value)[] Parameters) StatsFilter(ulong? noteID, string? timeModifier) =>
        Db.StatsFilter("DateStudied", timeModifier, "NoteID = $id", noteID);

    public static string GetContent(ulong noteID) =>
        Db.Scalar<string>("SELECT Content FROM Notes WHERE ID = $id;", ("$id", noteID)) ?? "";

    // --- Writes ---

    public static Note CreateNote(string name, ulong? folderID)
    {
        var syncID = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var id = Db.Insert(@"
            INSERT INTO Notes (SyncID, Name, FolderID, Content, CreatedAt, UpdatedAt)
            VALUES ($syncId, $name, $folderId, '', $now, $now);",
            ("$syncId", syncID), ("$name", name), ("$folderId", folderID), ("$now", WriteTime(now)));

        return new Note(id, syncID, name, folderID, 0, now.ToLocalTime(), now.ToLocalTime());
    }

    /// <summary> Saves the content and returns the new edited time (local). </summary>
    public static DateTime SaveContent(ulong noteID, string content)
    {
        var now = DateTime.UtcNow;

        Db.Execute("UPDATE Notes SET Content = $content, UpdatedAt = $now WHERE ID = $id;",
            ("$content", content), ("$now", WriteTime(now)), ("$id", noteID));

        return now.ToLocalTime();
    }

    public static void RenameNote(ulong noteID, string name) =>
        Db.Execute("UPDATE Notes SET Name = $name WHERE ID = $id;", ("$name", name), ("$id", noteID));

    public static void DeleteNote(ulong noteID)
    {
        Db.InTransaction((connection, transaction) =>
            Db.Execute(connection, transaction, "DELETE FROM NoteStats WHERE NoteID = $id; DELETE FROM Notes WHERE ID = $id;", ("$id", noteID)));
    }

    // --- Helpers ---

    private static string WriteTime(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ReadTime(string raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime()
            : DateTime.Now;
}
