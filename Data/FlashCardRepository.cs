using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ReviFlash.Models;

namespace ReviFlash.Data;

public static class FlashCardRepository
{
    private sealed record TrueFalseAnswerPayload(bool CorrectAnswerIsTrue, string TrueLabel, string FalseLabel);

    private static void ValidateDeckId(ulong deckID)
    {
        if (deckID == 0 || deckID == ulong.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(deckID), "Deck ID must be a valid persisted deck identifier.");
        }
    }

    public static void SaveNewDeck(FlashCardDeck deck)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Decks (Name) VALUES ($name);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("$name", deck.Name);

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        deck.AssignDatabaseID((ulong)newID);
    }

    public static void SaveNewCard(FlashCard card, ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Cards (DeckID, CardType, Front, Back, Answer) 
            VALUES ($deckId, $cardType, $front, $back, $answer);
            SELECT last_insert_rowid();
        ";

        string cardType = card.GetType().Name;

        command.Parameters.AddWithValue("$deckId", deckID);
        command.Parameters.AddWithValue("$cardType", cardType);
        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);
        command.Parameters.AddWithValue("$answer", BuildAnswerPayload(card));

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        card.AssignDatabaseID((ulong)newID);

        if (card is MultiFlashCard multiCard)
        {
            SaveCardOptions((ulong)newID, multiCard.Options, connection);
        }

        if (card is MatchFlashCard matchCard)
        {
            SaveMatchPairs((ulong)newID, matchCard.Options, connection);
        }
    }

    public static List<FlashCardDeck> GetAllDecks()
    {
        var decks = new List<FlashCardDeck>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT d.ID, d.Name, COUNT(c.ID) as CardCount
        FROM Decks d
        LEFT JOIN Cards c ON d.ID = c.DeckID
        GROUP BY d.ID, d.Name;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string name = reader.GetString(1);
            int cardCount = reader.GetInt32(2);

            decks.Add(new FlashCardDeck(name, id, cardCount));
        }
        return decks;
    }

    public static void SaveNewStudyGroup(StudyGroup group)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO StudyGroups (Name) VALUES ($name);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("$name", group.Name);

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        group.AssignDatabaseID((ulong)newID);
    }

    public static List<StudyGroup> GetAllStudyGroups()
    {
        var groups = new List<StudyGroup>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT g.ID, g.Name,
                   COUNT(DISTINCT gd.DeckID) AS DeckCount,
                   COALESCE(SUM(COALESCE(dc.CardCount, 0)), 0) AS CardCount
            FROM StudyGroups g
            LEFT JOIN StudyGroupDecks gd ON g.ID = gd.StudyGroupID
            LEFT JOIN (
                SELECT DeckID, COUNT(*) AS CardCount
                FROM Cards
                GROUP BY DeckID
            ) dc ON gd.DeckID = dc.DeckID
            GROUP BY g.ID, g.Name
            ORDER BY g.Name COLLATE NOCASE;
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string name = reader.GetString(1);
            int deckCount = reader.GetInt32(2);
            int cardCount = reader.GetInt32(3);

            groups.Add(new StudyGroup(name, id, deckCount, cardCount));
        }

        return groups;
    }

    public static void UpdateStudyGroup(StudyGroup group)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE StudyGroups SET Name = $name WHERE ID = $id;";

        command.Parameters.AddWithValue("$name", group.Name);
        command.Parameters.AddWithValue("$id", group.ID);

        command.ExecuteNonQuery();
    }

    public static void DeleteStudyGroup(ulong groupID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM StudyGroups WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", groupID);

        command.ExecuteNonQuery();
    }

    public static void AddDeckToStudyGroup(ulong groupID, ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO StudyGroupDecks (StudyGroupID, DeckID)
            VALUES ($groupId, $deckId);
        ";

        command.Parameters.AddWithValue("$groupId", groupID);
        command.Parameters.AddWithValue("$deckId", deckID);

        command.ExecuteNonQuery();
    }

    public static void RemoveDeckFromStudyGroup(ulong groupID, ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM StudyGroupDecks
            WHERE StudyGroupID = $groupId AND DeckID = $deckId;
        ";

        command.Parameters.AddWithValue("$groupId", groupID);
        command.Parameters.AddWithValue("$deckId", deckID);

        command.ExecuteNonQuery();
    }

    public static void SetStudyGroupDecks(ulong groupID, IEnumerable<ulong> deckIDs)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var clearCommand = connection.CreateCommand();
        clearCommand.Transaction = transaction;
        clearCommand.CommandText = "DELETE FROM StudyGroupDecks WHERE StudyGroupID = $groupId;";
        clearCommand.Parameters.AddWithValue("$groupId", groupID);
        clearCommand.ExecuteNonQuery();

        foreach (var deckID in deckIDs.Distinct())
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = @"
                INSERT OR IGNORE INTO StudyGroupDecks (StudyGroupID, DeckID)
                VALUES ($groupId, $deckId);
            ";
            insertCommand.Parameters.AddWithValue("$groupId", groupID);
            insertCommand.Parameters.AddWithValue("$deckId", deckID);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static List<FlashCardDeck> GetDecksForStudyGroup(ulong groupID)
    {
        var decks = new List<FlashCardDeck>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT d.ID, d.Name, COUNT(c.ID) AS CardCount
            FROM Decks d
            INNER JOIN StudyGroupDecks gd ON d.ID = gd.DeckID
            LEFT JOIN Cards c ON d.ID = c.DeckID
            WHERE gd.StudyGroupID = $groupId
            GROUP BY d.ID, d.Name
            ORDER BY d.Name COLLATE NOCASE;
        ";
        command.Parameters.AddWithValue("$groupId", groupID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string name = reader.GetString(1);
            int cardCount = reader.GetInt32(2);

            decks.Add(new FlashCardDeck(name, id, cardCount));
        }

        return decks;
    }

    public static List<StudyGroup> GetStudyGroupsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        var groups = new List<StudyGroup>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT g.ID, g.Name,
                   COUNT(DISTINCT gd.DeckID) AS DeckCount,
                   COALESCE(SUM(COALESCE(dc.CardCount, 0)), 0) AS CardCount
            FROM StudyGroups g
            INNER JOIN StudyGroupDecks gd ON g.ID = gd.StudyGroupID
            LEFT JOIN (
                SELECT DeckID, COUNT(*) AS CardCount
                FROM Cards
                GROUP BY DeckID
            ) dc ON gd.DeckID = dc.DeckID
            WHERE gd.DeckID = $deckId
            GROUP BY g.ID, g.Name
            ORDER BY g.Name COLLATE NOCASE;
        ";
        command.Parameters.AddWithValue("$deckId", deckID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string name = reader.GetString(1);
            int deckCount = reader.GetInt32(2);
            int cardCount = reader.GetInt32(3);

            groups.Add(new StudyGroup(name, id, deckCount, cardCount));
        }

        return groups;
    }

    public static List<FlashCard> GetCardsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        var cards = new List<FlashCard>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ID, CardType, Front, Back, Answer FROM Cards WHERE DeckID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string cardType = reader.GetString(1);
            string front = reader.GetString(2);
            string back = reader.GetString(3);
            string? answer = reader.IsDBNull(4) ? null : reader.GetString(4);

            // Polymorphic Instantiation based on the database flag
            FlashCard card = cardType switch
            {
                nameof(TypeFlashCard) => CreateTypeCard(front, back, answer, id),
                nameof(FlipFlashCard) => CreateFlipCard(front, back, id),
                nameof(MultiFlashCard) => CreateMultiCard(front, back, id, connection),
                nameof(MatchFlashCard) => CreateMatchCard(front, back, id, connection),
                nameof(TrueFalseFlashCard) => CreateTrueFalseCard(front, back, answer, id),
                _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
            };

            cards.Add(card);
        }

        return cards;
    }

    public static void UpdateDeck(FlashCardDeck deck)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Decks SET Name = $name WHERE ID = $id;";

        command.Parameters.AddWithValue("$name", deck.Name);
        command.Parameters.AddWithValue("$id", deck.ID);

        command.ExecuteNonQuery();
    }

    public static void UpdateDeckStats(ulong deckID, int correct, int total, int timeTakenSeconds)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO DeckStats (DeckId, CorrectCount, TotalAttempts, TimeTakenSeconds)
            VALUES (@deckID, @correct, @total, @timeTakenSeconds)
            ON CONFLICT(DeckId, DateChecked) DO UPDATE SET
                CorrectCount = CorrectCount + excluded.CorrectCount,
                TotalAttempts = TotalAttempts + excluded.TotalAttempts,
                TimeTakenSeconds = TimeTakenSeconds + excluded.TimeTakenSeconds
        ";

        command.Parameters.AddWithValue("@deckID", deckID);
        command.Parameters.AddWithValue("@correct", correct);
        command.Parameters.AddWithValue("@total", total);
        command.Parameters.AddWithValue("@timeTakenSeconds", timeTakenSeconds);

        command.ExecuteNonQuery();
    }

    public static (int correct, int total, int timeTakenSeconds) GetStats(ulong? deckID = null, string? timeModifier = null)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        string sql = @"
            SELECT 
                COALESCE(SUM(CorrectCount), 0) as TotalCorrect, 
                COALESCE(SUM(TotalAttempts), 0) as TotalTotal,
                COALESCE(SUM(TimeTakenSeconds), 0) as TotalTimeTakenSeconds
            FROM DeckStats 
            WHERE 1=1";

        if (deckID.HasValue)
        {
            sql += " AND DeckId = @deckId";
            command.Parameters.AddWithValue("@deckId", deckID.Value);
        }

        if (!string.IsNullOrEmpty(timeModifier))
        {
            sql += " AND DateChecked >= DATE('now', @timeModifier)";
            command.Parameters.AddWithValue("@timeModifier", timeModifier);
        }

        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            int correct = reader.GetInt32(0);
            int total = reader.GetInt32(1);
            int timeTakenSeconds = reader.GetInt32(2);
            return new(correct, total, timeTakenSeconds);
        }

        return (0, 0, 0);
    }

    public static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetStatsByDate(ulong? deckID = null, string? timeModifier = null)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        string sql = @"
            SELECT
                DateChecked,
                COALESCE(SUM(CorrectCount), 0) as TotalCorrect,
                COALESCE(SUM(TotalAttempts), 0) as TotalTotal,
                COALESCE(SUM(TimeTakenSeconds), 0) as TotalTimeTakenSeconds
            FROM DeckStats
            WHERE 1=1";

        if (deckID.HasValue)
        {
            sql += " AND DeckId = @deckId";
            command.Parameters.AddWithValue("@deckId", deckID.Value);
        }

        if (!string.IsNullOrEmpty(timeModifier))
        {
            sql += " AND DateChecked >= DATE('now', @timeModifier)";
            command.Parameters.AddWithValue("@timeModifier", timeModifier);
        }

        sql += " GROUP BY DateChecked ORDER BY DateChecked ASC;";
        command.CommandText = sql;

        var stats = new List<(DateOnly date, int correct, int total, int timeTakenSeconds)>();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var date = DateOnly.Parse(reader.GetString(0));
            int correct = reader.GetInt32(1);
            int total = reader.GetInt32(2);
            int timeTakenSeconds = reader.GetInt32(3);
            stats.Add((date, correct, total, timeTakenSeconds));
        }

        return stats;
    }

    public static int GetBestAnswerStreak(string targetType, ulong targetId)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT BestStreak
            FROM AnswerStreaks
            WHERE TargetType = $type AND TargetId = $id;
        ";
        command.Parameters.AddWithValue("$type", targetType);
        command.Parameters.AddWithValue("$id", targetId);

        var result = command.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public static void UpdateBestAnswerStreak(string targetType, ulong targetId, int bestStreak)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
            INSERT OR IGNORE INTO AnswerStreaks (TargetType, TargetId, BestStreak)
            VALUES ($type, $id, $best);
        ";
        insertCommand.Parameters.AddWithValue("$type", targetType);
        insertCommand.Parameters.AddWithValue("$id", targetId);
        insertCommand.Parameters.AddWithValue("$best", bestStreak);
        insertCommand.ExecuteNonQuery();

        var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = @"
            UPDATE AnswerStreaks
            SET BestStreak = CASE WHEN $best > BestStreak THEN $best ELSE BestStreak END
            WHERE TargetType = $type AND TargetId = $id;
        ";
        updateCommand.Parameters.AddWithValue("$type", targetType);
        updateCommand.Parameters.AddWithValue("$id", targetId);
        updateCommand.Parameters.AddWithValue("$best", bestStreak);
        updateCommand.ExecuteNonQuery();
    }

    public static int GetCardCount(ulong? deckID = null)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        var sql = "SELECT COUNT(*) FROM Cards";
        if (deckID.HasValue)
        {
            sql += " WHERE DeckID = $deckId";
            command.Parameters.AddWithValue("$deckId", deckID.Value);
        }

        command.CommandText = sql;
        var result = command.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public static void UpdateCard(FlashCard card)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        // Ensure the CardType column is updated when changing card subclasses
        command.CommandText = "UPDATE Cards SET CardType = $cardType, Front = $front, Back = $back, Answer = $answer WHERE ID = $id;";

        command.Parameters.AddWithValue("$cardType", card.GetType().Name);

        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);
        command.Parameters.AddWithValue("$answer", BuildAnswerPayload(card));
        command.Parameters.AddWithValue("$id", card.ID);

        command.ExecuteNonQuery();
        // Always clear any existing option/pair rows for this card —
        // this ensures switching between card types removes stale data.
        var clearOptions = connection.CreateCommand();
        clearOptions.CommandText = "DELETE FROM CardOptions WHERE CardID = $cardId;";
        clearOptions.Parameters.AddWithValue("$cardId", card.ID);
        clearOptions.ExecuteNonQuery();

        var clearPairs = connection.CreateCommand();
        clearPairs.CommandText = "DELETE FROM MatchCardPairs WHERE CardID = $cardId;";
        clearPairs.Parameters.AddWithValue("$cardId", card.ID);
        clearPairs.ExecuteNonQuery();

        if (card is MultiFlashCard multiCard)
        {
            SaveCardOptions(card.ID, multiCard.Options, connection);
        }

        if (card is MatchFlashCard matchCard)
        {
            SaveMatchPairs(card.ID, matchCard.Options, connection);
        }
    }

    public static void DeleteDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Decks WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", deckID);

        command.ExecuteNonQuery();
    }

    public static void DeleteCard(ulong cardID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var deleteOptions = connection.CreateCommand();
        deleteOptions.CommandText = "DELETE FROM CardOptions WHERE CardID = $id;";
        deleteOptions.Parameters.AddWithValue("$id", cardID);
        deleteOptions.ExecuteNonQuery();

        var deletePairs = connection.CreateCommand();
        deletePairs.CommandText = "DELETE FROM MatchCardPairs WHERE CardID = $id;";
        deletePairs.Parameters.AddWithValue("$id", cardID);
        deletePairs.ExecuteNonQuery();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cards WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", cardID);

        command.ExecuteNonQuery();
    }

    public static void DeleteAllStats()
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DeckStats;";
        command.ExecuteNonQuery();
    }

    public static void DeleteStatsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DeckStats WHERE DeckID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckID);
        command.ExecuteNonQuery();
    }

    private static void SaveCardOptions(ulong cardID, List<(string optionText, bool isCorrect)> options, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        for (int i = 0; i < options.Count; i++)
        {
            var (optionText, isCorrect) = options[i];
            var optionCommand = connection.CreateCommand();
            optionCommand.CommandText = @"
                INSERT INTO CardOptions (CardID, OptionIndex, OptionText, IsCorrect)
                VALUES ($cardId, $index, $text, $isCorrect);
            ";
            optionCommand.Parameters.AddWithValue("$cardId", cardID);
            optionCommand.Parameters.AddWithValue("$index", i);
            optionCommand.Parameters.AddWithValue("$text", optionText);
            optionCommand.Parameters.AddWithValue("$isCorrect", isCorrect ? 1 : 0);
            optionCommand.ExecuteNonQuery();
        }
    }

    private static List<(string optionText, bool isCorrect)> GetCardOptions(ulong cardID, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var options = new List<(string optionText, bool isCorrect)>();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT OptionText, IsCorrect
            FROM CardOptions
            WHERE CardID = $cardId
            ORDER BY OptionIndex ASC;
        ";
        command.Parameters.AddWithValue("$cardId", cardID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string optionText = reader.GetString(0);
            bool isCorrect = reader.GetInt32(1) == 1;
            options.Add((optionText, isCorrect));
        }

        return options;
    }

    private static FlashCard CreateFlipCard(string front, string back, ulong id)
    {
        return new FlipFlashCard(front, back, id);
    }

    private static FlashCard CreateTypeCard(string front, string back, string? answer, ulong id)
    {
        return new TypeFlashCard(front, back, answer, id);
    }

    private static FlashCard CreateTrueFalseCard(string front, string back, string? answer, ulong id)
    {
        return BuildTrueFalseCard(front, back, answer, id);
    }

    private static FlashCard CreateMultiCard(string front, string back, ulong id, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var options = GetCardOptions(id, connection);
        return new MultiFlashCard(front, back, options, id);
    }

    private static FlashCard CreateMatchCard(string front, string back, ulong id, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var pairs = GetMatchPairs(id, connection);
        return new MatchFlashCard(front, back, pairs, id);
    }

    private static void SaveMatchPairs(ulong cardID, List<(string leftText, string rightText)> pairs, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            var (leftText, rightText) = pairs[i];
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MatchCardPairs (CardID, PairIndex, LeftText, RightText)
                VALUES ($cardId, $index, $leftText, $rightText);
            ";
            command.Parameters.AddWithValue("$cardId", cardID);
            command.Parameters.AddWithValue("$index", i);
            command.Parameters.AddWithValue("$leftText", leftText);
            command.Parameters.AddWithValue("$rightText", rightText);
            command.ExecuteNonQuery();
        }
    }

    private static List<(string leftText, string rightText)> GetMatchPairs(ulong cardID, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var pairs = new List<(string leftText, string rightText)>();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT LeftText, RightText
            FROM MatchCardPairs
            WHERE CardID = $cardId
            ORDER BY PairIndex ASC;
        ";
        command.Parameters.AddWithValue("$cardId", cardID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            pairs.Add((reader.GetString(0), reader.GetString(1)));
        }

        return pairs;
    }

    private static object BuildAnswerPayload(FlashCard card)
    {
        return card switch
        {
            TypeFlashCard typeCard => typeCard.Answer,
            TrueFalseFlashCard trueFalseCard => JsonSerializer.Serialize(
                new TrueFalseAnswerPayload(trueFalseCard.CorrectAnswerIsTrue, trueFalseCard.TrueLabel, trueFalseCard.FalseLabel)),
            _ => DBNull.Value,
        };
    }

    private static TrueFalseFlashCard BuildTrueFalseCard(string front, string back, string? answerPayload, ulong id)
    {
        var (correctAnswerIsTrue, trueLabel, falseLabel) = ParseTrueFalsePayload(answerPayload);
        return new TrueFalseFlashCard(front, back, correctAnswerIsTrue, trueLabel, falseLabel, id);
    }

    private static (bool correctAnswerIsTrue, string trueLabel, string falseLabel) ParseTrueFalsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (true, "True", "False");
        }

        if (bool.TryParse(payload, out var boolAnswer))
        {
            return (boolAnswer, "True", "False");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TrueFalseAnswerPayload>(payload);
            if (parsed is null)
            {
                return (true, "True", "False");
            }

            var trueLabel = string.IsNullOrWhiteSpace(parsed.TrueLabel) ? "True" : parsed.TrueLabel.Trim();
            var falseLabel = string.IsNullOrWhiteSpace(parsed.FalseLabel) ? "False" : parsed.FalseLabel.Trim();

            if (string.Equals(trueLabel, falseLabel, StringComparison.OrdinalIgnoreCase))
            {
                return (parsed.CorrectAnswerIsTrue, "True", "False");
            }

            return (parsed.CorrectAnswerIsTrue, trueLabel, falseLabel);
        }
        catch (JsonException)
        {
            return (true, "True", "False");
        }
    }
}