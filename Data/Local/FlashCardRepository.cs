using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

public static class FlashCardRepository
{
    /// <summary> AnswerStreaks.TargetType for a deck's and a study group's best streak. </summary>
    internal const string DeckStreakTarget = "Deck", GroupStreakTarget = "Group";

    private const string GroupDecksCondition = "DeckId IN (SELECT DeckID FROM StudyGroupDecks WHERE StudyGroupID = $id)";
    private const string FolderDecksCondition = $"DeckId IN (SELECT ID FROM Decks WHERE FolderID IN {Db.FolderSubtreeIds})";

    private static void ValidateDeckId(ulong deckID)
    {
        if (deckID == 0 || deckID == ulong.MaxValue) throw new ArgumentOutOfRangeException(nameof(deckID), "Deck ID must be a valid persisted deck identifier.");
    }

    // -- Saves ---

    /// <param name="folderID"> Folder the group is filed into; null for the main menu. </param>
    public static void SaveNewStudyGroup(StudyGroup group, IEnumerable<ulong> deckIDs, ulong? folderID = null)
    {
        ulong newID = Db.InTransaction((connection, transaction) =>
        {
            ulong id = Db.Insert(connection, transaction, "INSERT INTO StudyGroups (Name, FolderID) VALUES ($name, $folderId);",
                ("$name", group.Name), ("$folderId", folderID));
            ReplaceStudyGroupDecks(connection, transaction, id, deckIDs);
            return id;
        });

        group.AssignDatabaseID(newID);
        group.FolderID = folderID;
    }

    /// <param name="folderID"> Folder the deck is filed into; null for the main menu. </param>
    public static void SaveNewDeck(FlashCardDeck deck, ulong? folderID = null)
    {
        using var connection = Db.Open();
        deck.AssignDatabaseID(DeckRepository.InsertDeck(connection, null, deck.Name, folderID));
        deck.FolderID = folderID;
    }

    public static void SaveNewCard(FlashCard card, ulong deckID)
    {
        ValidateDeckId(deckID);

        card.AssignDatabaseID(Db.InTransaction((connection, transaction) => DeckRepository.InsertCard(connection, transaction, deckID, card)));
    }

    // --- Get ---

    public static List<StudyGroup> GetAllStudyGroups()
    {
        return Db.Query(@"
            SELECT g.ID, g.Name, g.FolderID,
                   COUNT(DISTINCT gd.DeckID) AS DeckCount,
                   COALESCE(SUM(COALESCE(dc.CardCount, 0)), 0) AS CardCount,
                   COALESCE(SUM(COALESCE(ds.StudySeconds, 0)), 0) AS StudySeconds,
                   MAX(ds.LastStudied) AS LastStudied
            FROM StudyGroups g
            LEFT JOIN StudyGroupDecks gd ON g.ID = gd.StudyGroupID
            LEFT JOIN (
                SELECT DeckID, COUNT(*) AS CardCount
                FROM Cards
                GROUP BY DeckID
            ) dc ON gd.DeckID = dc.DeckID
            LEFT JOIN (
                SELECT DeckId, SUM(TimeTakenSeconds) AS StudySeconds, MAX(DateChecked) AS LastStudied
                FROM DeckStats
                GROUP BY DeckId
            ) ds ON gd.DeckID = ds.DeckId
            GROUP BY g.ID, g.Name, g.FolderID
            ORDER BY g.Name COLLATE NOCASE;",
            reader => new StudyGroup(
                reader.GetString(1),
                (ulong)reader.GetInt64(0),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.IsDBNull(2) ? null : (ulong)reader.GetInt64(2),
                reader.GetInt32(5),
                Db.ReadNullableDate(reader, 6)));
    }

    public static List<FlashCardDeck> GetAllDecks()
    {
        return Db.Query(@"
            SELECT d.ID, d.Name, d.FolderID,
                   COALESCE(c.CardCount, 0) AS CardCount,
                   COALESCE(s.StudySeconds, 0) AS StudySeconds,
                   s.LastStudied
            FROM Decks d
            LEFT JOIN (
                SELECT DeckID, COUNT(*) AS CardCount
                FROM Cards
                GROUP BY DeckID
            ) c ON c.DeckID = d.ID
            LEFT JOIN (
                SELECT DeckId, SUM(TimeTakenSeconds) AS StudySeconds, MAX(DateChecked) AS LastStudied
                FROM DeckStats
                GROUP BY DeckId
            ) s ON s.DeckId = d.ID
            ORDER BY d.Name COLLATE NOCASE;",
            reader => new FlashCardDeck(
                reader.GetString(1),
                (ulong)reader.GetInt64(0),
                reader.GetInt32(3),
                reader.IsDBNull(2) ? null : (ulong)reader.GetInt64(2),
                reader.GetInt32(4),
                Db.ReadNullableDate(reader, 5)));
    }

    public static List<FlashCardDeck> GetDecksForStudyGroup(ulong groupID)
    {
        return Db.Query(@"
            SELECT d.ID, d.Name, COUNT(c.ID) AS CardCount
            FROM Decks d
            INNER JOIN StudyGroupDecks gd ON d.ID = gd.DeckID
            LEFT JOIN Cards c ON d.ID = c.DeckID
            WHERE gd.StudyGroupID = $groupId
            GROUP BY d.ID, d.Name
            ORDER BY d.Name COLLATE NOCASE;",
            reader => new FlashCardDeck(reader.GetString(1), (ulong)reader.GetInt64(0), reader.GetInt32(2)),
            ("$groupId", groupID));
    }

    public static List<FlashCard> GetCardsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = Db.Open();
        return DeckRepository.LoadCards(connection, deckID);
    }

    public static (int correct, int total, int timeTakenSeconds) GetStats(ulong? deckID = null, string? timeModifier = null) =>
        GetDeckStatsTotals(Db.StatsFilter("DateChecked", timeModifier, "DeckId = $id", deckID));

    public static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetStatsByDate(ulong? deckID = null, string? timeModifier = null) =>
        GetDeckStatsByDate(Db.StatsFilter("DateChecked", timeModifier, "DeckId = $id", deckID));

    /// <summary> Review totals across every deck in the group. </summary>
    public static (int correct, int total, int timeTakenSeconds) GetStudyGroupStats(ulong groupID, string? timeModifier = null) =>
        GetDeckStatsTotals(Db.StatsFilter("DateChecked", timeModifier, GroupDecksCondition, groupID));

    /// <summary> Review totals per day across every deck in the group, oldest first. </summary>
    public static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetStudyGroupStatsByDate(ulong groupID, string? timeModifier = null) =>
        GetDeckStatsByDate(Db.StatsFilter("DateChecked", timeModifier, GroupDecksCondition, groupID));

    /// <summary> Review stats for every set in a folder and its subfolders. </summary>
    public static (int correct, int total, int timeTakenSeconds) GetFolderStats(ulong folderID, string? timeModifier = null) =>
        GetDeckStatsTotals(Db.StatsFilter("DateChecked", timeModifier, FolderDecksCondition, folderID));

    public static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetFolderStatsByDate(ulong folderID, string? timeModifier = null) =>
        GetDeckStatsByDate(Db.StatsFilter("DateChecked", timeModifier, FolderDecksCondition, folderID));

    private static (int correct, int total, int timeTakenSeconds) GetDeckStatsTotals((string Sql, (string Name, object? Value)[] Parameters) filter)
    {
        return Db.Query(@"
            SELECT
                COALESCE(SUM(CorrectCount), 0) as TotalCorrect,
                COALESCE(SUM(TotalAttempts), 0) as TotalTotal,
                COALESCE(SUM(TimeTakenSeconds), 0) as TotalTimeTakenSeconds
            FROM DeckStats
            WHERE 1=1" + filter.Sql + ";",
            reader => (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)),
            filter.Parameters).FirstOrDefault();
    }

    private static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetDeckStatsByDate((string Sql, (string Name, object? Value)[] Parameters) filter)
    {
        return Db.Query(@"
            SELECT
                DateChecked,
                COALESCE(SUM(CorrectCount), 0) as TotalCorrect,
                COALESCE(SUM(TotalAttempts), 0) as TotalTotal,
                COALESCE(SUM(TimeTakenSeconds), 0) as TotalTimeTakenSeconds
            FROM DeckStats
            WHERE 1=1" + filter.Sql + " GROUP BY DateChecked ORDER BY DateChecked ASC;",
            reader => (Db.ParseDay(reader.GetString(0)), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)),
            filter.Parameters);
    }

    /// <summary> Seconds of review per set within the time period, for "Where your time went". </summary>
    public static List<(ulong deckID, int seconds)> GetStudyTimeByDeck(string? timeModifier = null)
    {
        var filter = Db.StatsFilter("DateChecked", timeModifier);
        return Db.Query("SELECT DeckId, COALESCE(SUM(TimeTakenSeconds), 0) FROM DeckStats WHERE 1=1" + filter.Sql + " GROUP BY DeckId;",
            reader => ((ulong)reader.GetInt64(0), reader.GetInt32(1)),
            filter.Parameters);
    }

    // --- Editing time ---

    /// <summary> Adds time spent editing a set to today's total. </summary>
    public static void AddEditingTime(ulong deckID, int seconds)
    {
        if (seconds <= 0) return;

        Db.Execute(@"
            INSERT INTO DeckEditStats (DeckId, Seconds, DateEdited) VALUES ($id, $seconds, $today)
            ON CONFLICT(DeckId, DateEdited) DO UPDATE SET Seconds = Seconds + excluded.Seconds;",
            ("$id", deckID), ("$seconds", seconds), ("$today", Db.Today()));
    }

    /// <summary> Seconds spent editing one set, or every set when null, within the time period. </summary>
    public static int GetEditingSeconds(ulong? deckID, string? timeModifier) =>
        GetEditingSeconds(Db.StatsFilter("DateEdited", timeModifier, "DeckId = $id", deckID));

    public static int GetStudyGroupEditingSeconds(ulong groupID, string? timeModifier) =>
        GetEditingSeconds(Db.StatsFilter("DateEdited", timeModifier, GroupDecksCondition, groupID));

    public static int GetFolderEditingSeconds(ulong folderID, string? timeModifier) =>
        GetEditingSeconds(Db.StatsFilter("DateEdited", timeModifier, FolderDecksCondition, folderID));

    private static int GetEditingSeconds((string Sql, (string Name, object? Value)[] Parameters) filter) =>
        Db.Scalar<int>("SELECT COALESCE(SUM(Seconds), 0) FROM DeckEditStats WHERE 1=1" + filter.Sql + ";", filter.Parameters);

    /// <summary> Editing seconds per day, oldest first, for one set, or every set when null. </summary>
    public static List<(DateOnly date, int seconds)> GetEditingTimeByDate(ulong? deckID, string? timeModifier) =>
        GetEditingTimeByDate(Db.StatsFilter("DateEdited", timeModifier, "DeckId = $id", deckID));

    public static List<(DateOnly date, int seconds)> GetStudyGroupEditingTimeByDate(ulong groupID, string? timeModifier) =>
        GetEditingTimeByDate(Db.StatsFilter("DateEdited", timeModifier, GroupDecksCondition, groupID));

    public static List<(DateOnly date, int seconds)> GetFolderEditingTimeByDate(ulong folderID, string? timeModifier) =>
        GetEditingTimeByDate(Db.StatsFilter("DateEdited", timeModifier, FolderDecksCondition, folderID));

    private static List<(DateOnly date, int seconds)> GetEditingTimeByDate((string Sql, (string Name, object? Value)[] Parameters) filter) =>
        Db.Query("SELECT DateEdited, SUM(Seconds) FROM DeckEditStats WHERE 1=1" + filter.Sql + " GROUP BY DateEdited ORDER BY DateEdited;",
            reader => (Db.ParseDay(reader.GetString(0)), reader.GetInt32(1)),
            filter.Parameters);

    public static List<(ulong deckID, int seconds)> GetEditingTimeByDeck(string? timeModifier)
    {
        var filter = Db.StatsFilter("DateEdited", timeModifier);
        return Db.Query("SELECT DeckId, SUM(Seconds) FROM DeckEditStats WHERE 1=1" + filter.Sql + " GROUP BY DeckId;",
            reader => ((ulong)reader.GetInt64(0), reader.GetInt32(1)),
            filter.Parameters);
    }

    public static int GetBestAnswerStreak(string targetType, ulong targetId) =>
        Db.Scalar<int>("SELECT BestStreak FROM AnswerStreaks WHERE TargetType = $type AND TargetId = $id;",
            ("$type", targetType), ("$id", targetId));

    public static int GetCardCount(ulong? deckID = null) =>
        deckID.HasValue
            ? Db.Scalar<int>("SELECT COUNT(*) FROM Cards WHERE DeckID = $deckId;", ("$deckId", deckID.Value))
            : Db.Scalar<int>("SELECT COUNT(*) FROM Cards;");

    public static int GetStudyGroupCardCount(ulong groupID) =>
        Db.Scalar<int>("SELECT COUNT(*) FROM Cards WHERE DeckID IN (SELECT DeckID FROM StudyGroupDecks WHERE StudyGroupID = $groupId);",
            ("$groupId", groupID));

    // --- Updates ---

    /// <summary> Renames the group and replaces its decks. </summary>
    public static void UpdateStudyGroup(StudyGroup group, IEnumerable<ulong> deckIDs)
    {
        Db.InTransaction((connection, transaction) =>
        {
            Db.Execute(connection, transaction, "UPDATE StudyGroups SET Name = $name WHERE ID = $id;", ("$name", group.Name), ("$id", group.ID));
            ReplaceStudyGroupDecks(connection, transaction, group.ID, deckIDs);
        });
    }

    public static void UpdateDeck(FlashCardDeck deck) =>
        Db.Execute("UPDATE Decks SET Name = $name WHERE ID = $id;", ("$name", deck.Name), ("$id", deck.ID));

    /// <summary> Adds a review session's results to today's stats, one entry per deck, along with each answer given. </summary>
    public static void UpdateDeckStats(IReadOnlyCollection<(ulong deckID, int correct, int total, int timeTakenSeconds)> results,
        IReadOnlyCollection<CardResult>? answers = null)
    {
        foreach (var result in results) ValidateDeckId(result.deckID);
        if (results.Count == 0 && (answers is null || answers.Count == 0)) return;

        Db.InTransaction((connection, transaction) =>
        {
            foreach (var answer in answers ?? [])
            {
                Db.Execute(connection, transaction, @"
                    INSERT INTO CardResults (CardID, Part, AnsweredAt, Correct, Seconds)
                    SELECT $cardId, $part, $answeredAt, $correct, $seconds
                    WHERE EXISTS (SELECT 1 FROM Cards WHERE ID = $cardId);",
                    ("$cardId", answer.CardID), ("$part", answer.Part), ("$answeredAt", Db.LocalTime(answer.AnsweredAt)),
                    ("$correct", answer.Correct ? 1 : 0), ("$seconds", answer.Seconds));
            }

            foreach (var (deckID, correct, total, timeTakenSeconds) in results)
            {
                Db.Execute(connection, transaction, @"
                    INSERT INTO DeckStats (DeckId, CorrectCount, TotalAttempts, TimeTakenSeconds, DateChecked)
                    VALUES ($deckId, $correct, $total, $timeTakenSeconds, $today)
                    ON CONFLICT(DeckId, DateChecked) DO UPDATE SET
                        CorrectCount = CorrectCount + excluded.CorrectCount,
                        TotalAttempts = TotalAttempts + excluded.TotalAttempts,
                        TimeTakenSeconds = TimeTakenSeconds + excluded.TimeTakenSeconds;",
                    ("$deckId", deckID), ("$correct", correct), ("$total", total), ("$timeTakenSeconds", timeTakenSeconds), ("$today", Db.Today()));
            }
        });
    }

    /// <summary> Every answer given for a card, oldest first. </summary>
    public static List<CardResult> GetCardResults(ulong cardID) =>
        Db.Query("SELECT CardID, Part, AnsweredAt, Correct, Seconds FROM CardResults WHERE CardID = $id ORDER BY AnsweredAt, ID;",
            reader => new CardResult((ulong)reader.GetInt64(0), reader.GetString(1), Db.ParseLocalTime(reader.GetString(2)), reader.GetInt32(3) == 1, reader.GetInt32(4)),
            ("$id", cardID));

    public static void UpdateCard(FlashCard card) =>
        Db.InTransaction((connection, transaction) => DeckRepository.UpdateCard(connection, transaction, card));

    /// <summary> Moves a card to another deck, keeping its answer history. </summary>
    public static void MoveCard(ulong cardID, ulong deckID)
    {
        ValidateDeckId(deckID);

        Db.Execute("UPDATE Cards SET DeckID = $deckId WHERE ID = $id;", ("$deckId", deckID), ("$id", cardID));
    }

    /// <summary> Saves a copy of a card into a deck, with no answer history, and returns the copy. </summary>
    public static FlashCard CopyCard(FlashCard card, ulong deckID)
    {
        ValidateDeckId(deckID);

        ulong copyID = Db.InTransaction((connection, transaction) => DeckRepository.InsertCard(connection, transaction, deckID, card));

        using var connection = Db.Open();
        return DeckRepository.LoadCards(connection, deckID).Single(copy => copy.ID == copyID);
    }

    public static void UpdateBestAnswerStreak(string targetType, ulong targetId, int bestStreak)
    {
        Db.Execute(@"
            INSERT INTO AnswerStreaks (TargetType, TargetId, BestStreak)
            VALUES ($type, $id, $best)
            ON CONFLICT(TargetType, TargetId) DO UPDATE SET BestStreak = MAX(BestStreak, excluded.BestStreak);",
            ("$type", targetType), ("$id", targetId), ("$best", bestStreak));
    }

    // --- Delete ---

    public static void DeleteStudyGroup(ulong groupID)
    {
        Db.InTransaction((connection, transaction) => Db.Execute(connection, transaction, @"
            DELETE FROM AnswerStreaks WHERE TargetType = $type AND TargetId = $id;
            DELETE FROM StudyGroups WHERE ID = $id;",
            ("$type", GroupStreakTarget), ("$id", groupID)));
    }

    public static void DeleteDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        Db.InTransaction((connection, transaction) => Db.Execute(connection, transaction, @"
            DELETE FROM AnswerStreaks WHERE TargetType = $type AND TargetId = $id;
            DELETE FROM Decks WHERE ID = $id;",
            ("$type", DeckStreakTarget), ("$id", deckID)));
    }

    public static void DeleteCard(ulong cardID) =>
        Db.Execute("DELETE FROM Cards WHERE ID = $id;", ("$id", cardID));

    public static void DeleteAllStats()
    {
        Db.InTransaction((connection, transaction) =>
            Db.Execute(connection, transaction, "DELETE FROM DeckStats; DELETE FROM DeckEditStats; DELETE FROM CardResults; DELETE FROM AnswerStreaks; DELETE FROM NoteStats;"));
    }

    public static void DeleteStatsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        Db.InTransaction((connection, transaction) => Db.Execute(connection, transaction, @"
            DELETE FROM DeckStats WHERE DeckID = $id;
            DELETE FROM DeckEditStats WHERE DeckId = $id;
            DELETE FROM CardResults WHERE CardID IN (SELECT ID FROM Cards WHERE DeckID = $id);
            DELETE FROM AnswerStreaks WHERE TargetType = $type AND TargetId = $id;",
            ("$type", DeckStreakTarget), ("$id", deckID)));
    }

    // --- Insert ---

    private static void ReplaceStudyGroupDecks(SqliteConnection connection, SqliteTransaction transaction, ulong groupID, IEnumerable<ulong> deckIDs)
    {
        Db.Execute(connection, transaction, "DELETE FROM StudyGroupDecks WHERE StudyGroupID = $groupId;", ("$groupId", groupID));

        foreach (var deckID in deckIDs.Distinct())
        {
            Db.Execute(connection, transaction, "INSERT OR IGNORE INTO StudyGroupDecks (StudyGroupID, DeckID) VALUES ($groupId, $deckId);",
                ("$groupId", groupID), ("$deckId", deckID));
        }
    }
}
