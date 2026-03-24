using System;
using System.Collections.Generic;
using ReviFlash.Models;

namespace ReviFlash.Data;

public static class FlashCardRepository
{
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
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Cards (DeckID, CardType, Front, Back) 
            VALUES ($deckId, $cardType, $front, $back);
            SELECT last_insert_rowid();
        ";

        string cardType = card.GetType().Name;

        command.Parameters.AddWithValue("$deckId", deckID);
        command.Parameters.AddWithValue("$cardType", cardType);
        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);

        long newID = (long)(command.ExecuteScalar() ?? long.MaxValue);
        card.AssignDatabaseID((ulong)newID);
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

    public static List<FlashCard> GetCardsForDeck(ulong deckID)
    {
        var cards = new List<FlashCard>();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ID, CardType, Front, Back FROM Cards WHERE DeckID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckID);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong id = (ulong)reader.GetInt64(0);
            string cardType = reader.GetString(1);
            string front = reader.GetString(2);
            string back = reader.GetString(3);

            // Polymorphic Instantiation based on the database flag
            FlashCard card = cardType switch
            {
                nameof(TypeFlashCard) => new TypeFlashCard(front, back, id),
                nameof(FlipFlashCard) => new FlipFlashCard(front, back, id),
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

    public static void UpdateCard(FlashCard card)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Cards SET Front = $front, Back = $back WHERE ID = $id;";

        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);
        command.Parameters.AddWithValue("$id", card.ID);

        command.ExecuteNonQuery();
    }

    public static void DeleteDeck(ulong deckID)
    {
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
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DeckStats WHERE DeckID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckID);
        command.ExecuteNonQuery();
    }
}