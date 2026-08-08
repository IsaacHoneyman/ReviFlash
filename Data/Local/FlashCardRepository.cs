using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReviFlash.Data.Online;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

public static class FlashCardRepository
{
    private static void ValidateDeckId(ulong deckID)
    {
        if (deckID == 0 || deckID == ulong.MaxValue) throw new ArgumentOutOfRangeException(nameof(deckID), "Deck ID must be a valid persisted deck identifier.");
    }

    // -- Saves ---

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

        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.Transaction = transaction;
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
        command.Parameters.AddWithValue("$answer", FlashCardFactory.BuildAnswerPayload(card));

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        card.AssignDatabaseID((ulong)newID);

        if (card is MultiFlashCard multiCard) SaveCardOptions((ulong)newID, multiCard.Options, connection, transaction);
        if (card is MatchFlashCard matchCard) SaveMatchPairs((ulong)newID, matchCard.Options, connection, transaction);

        transaction.Commit();
    }

    private static void SaveCardOptions(ulong cardID, List<(string optionText, bool isCorrect)> options, Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction)
    {
        for (int i = 0; i < options.Count; i++)
        {
            var (optionText, isCorrect) = options[i];
            var optionCommand = connection.CreateCommand();
            optionCommand.Transaction = transaction;
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

    private static void SaveMatchPairs(ulong cardID, List<(string leftText, string rightText)> pairs, Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            var (leftText, rightText) = pairs[i];
            var command = connection.CreateCommand();
            command.Transaction = transaction;
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

    // --- Get ---

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

    public static List<FlashCard> GetCardsForDeck(ulong deckID)
    {
        ValidateDeckId(deckID);

        var cards = new List<FlashCard>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var rawCards = new List<(ulong Id, string CardType, string Front, string Back, string? Answer)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT ID, CardType, Front, Back, Answer FROM Cards WHERE DeckID = $deckId ORDER BY ID ASC;";
            command.Parameters.AddWithValue("$deckId", deckID);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rawCards.Add((
                    (ulong)reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)
                ));
            }
        }

        if (rawCards.Count == 0) return cards;

        var cardIds = rawCards.Select(c => c.Id).ToList();
        var optionsMap = BatchGetCardOptions(cardIds, connection);
        var pairsMap = BatchGetMatchPairs(cardIds, connection);

        foreach (var (Id, CardType, Front, Back, Answer) in rawCards)
        {
            optionsMap.TryGetValue(Id, out var options);
            pairsMap.TryGetValue(Id, out var pairs);

            var safeOptions = options ?? [];
            var safePairs = pairs ?? [];

            FlashCard card = FlashCardFactory.CreateCard(
                CardType, Front, Back, Answer, Id, safeOptions, safePairs);
            cards.Add(card);
        }

        return cards;
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


    // --- Updates ---

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

    public static void UpdateCard(FlashCard card)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Cards SET CardType = $cardType, Front = $front, Back = $back, Answer = $answer WHERE ID = $id;";

        command.Parameters.AddWithValue("$cardType", card.GetType().Name);
        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);
        command.Parameters.AddWithValue("$answer", FlashCardFactory.BuildAnswerPayload(card));
        command.Parameters.AddWithValue("$id", card.ID);
        command.ExecuteNonQuery();

        var clearOptions = connection.CreateCommand();
        clearOptions.Transaction = transaction;
        clearOptions.CommandText = "DELETE FROM CardOptions WHERE CardID = $cardId;";
        clearOptions.Parameters.AddWithValue("$cardId", card.ID);
        clearOptions.ExecuteNonQuery();

        var clearPairs = connection.CreateCommand();
        clearPairs.Transaction = transaction;
        clearPairs.CommandText = "DELETE FROM MatchCardPairs WHERE CardID = $cardId;";
        clearPairs.Parameters.AddWithValue("$cardId", card.ID);
        clearPairs.ExecuteNonQuery();

        if (card is MultiFlashCard multiCard) SaveCardOptions(card.ID, multiCard.Options, connection, transaction);
        if (card is MatchFlashCard matchCard) SaveMatchPairs(card.ID, matchCard.Options, connection, transaction);

        transaction.Commit();
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

    // --- Delete ---

    public static void DeleteStudyGroup(ulong groupID)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM StudyGroups WHERE ID = $id;";
        command.Parameters.AddWithValue("$id", groupID);

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

    // --- Insert ---

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

    // --- Batch ---

    private static Dictionary<ulong, List<(string optionText, bool isCorrect)>> BatchGetCardOptions(List<ulong> cardIds, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var map = new Dictionary<ulong, List<(string optionText, bool isCorrect)>>();
        if (cardIds.Count == 0) return map;

        using var command = connection.CreateCommand();

        var parameterNames = cardIds.Select((_, i) => $"$id{i}").ToList();
        command.CommandText = $@"
        SELECT CardID, OptionText, IsCorrect
        FROM CardOptions
        WHERE CardID IN ({string.Join(", ", parameterNames)})
        ORDER BY CardID, OptionIndex ASC;";

        for (int i = 0; i < cardIds.Count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], cardIds[i]);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong cardId = (ulong)reader.GetInt64(0);
            string optionText = reader.GetString(1);
            bool isCorrect = reader.GetInt32(2) == 1;

            if (!map.TryGetValue(cardId, out var list))
            {
                list = [];
                map[cardId] = list;
            }

            list.Add((optionText, isCorrect));
        }

        return map;
    }

    private static Dictionary<ulong, List<(string leftText, string rightText)>> BatchGetMatchPairs(List<ulong> cardIds, Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var map = new Dictionary<ulong, List<(string leftText, string rightText)>>();
        if (cardIds.Count == 0) return map;

        using var command = connection.CreateCommand();

        var parameterNames = cardIds.Select((_, i) => $"$id{i}").ToList();
        command.CommandText = $@"
        SELECT CardID, LeftText, RightText
        FROM MatchCardPairs
        WHERE CardID IN ({string.Join(", ", parameterNames)})
        ORDER BY CardID, PairIndex ASC;";

        for (int i = 0; i < cardIds.Count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], cardIds[i]);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong cardId = (ulong)reader.GetInt64(0);
            string leftText = reader.GetString(1);
            string rightText = reader.GetString(2);

            if (!map.TryGetValue(cardId, out var list))
            {
                list = new List<(string, string)>();
                map[cardId] = list;
            }

            list.Add((leftText, rightText));
        }

        return map;
    }

    // ---Extensions ---

    public static IEnumerable<FlashCardDeckMetadata> FilterBySearch(this IEnumerable<FlashCardDeckMetadata> decks, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return decks;

        var parsedText = text.Trim();
        return decks.Where(d => d.MatchesSearch(parsedText));
    }

    private static bool MatchesSearch(this FlashCardDeckMetadata deck, string parsedText)
    {
        return deck.Title?.Contains(parsedText, StringComparison.OrdinalIgnoreCase) ?? false ||
               deck.CardCount.ToString().Contains(parsedText, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<FlashCardDeck> FilterBySearch(this IEnumerable<FlashCardDeck> decks, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return decks;

        var parsedText = text.Trim();
        return decks.Where(d => d.MatchesSearch(parsedText));
    }

    private static bool MatchesSearch(this FlashCardDeck deck, string parsedText)
    {
        return deck.Name.Contains(parsedText, StringComparison.OrdinalIgnoreCase) ||
               deck.CardCount.ToString().Contains(parsedText, StringComparison.OrdinalIgnoreCase);
    }
}