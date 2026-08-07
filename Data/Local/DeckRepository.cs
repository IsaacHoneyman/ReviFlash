using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

public static class DeckRepository
{
    // Read

    public static string? GetDeckName(SqliteConnection connection, ulong deckId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Decks WHERE ID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);

        var result = command.ExecuteScalar();
        return result as string;
    }

    public static List<CardExportEntry> LoadDeckCards(SqliteConnection connection, ulong deckId)
    {
        var cards = new List<CardExportEntry>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ID, CardType, Front, Back, Answer
            FROM Cards
            WHERE DeckID = $deckId
            ORDER BY ID ASC;";
        command.Parameters.AddWithValue("$deckId", deckId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ulong cardId = (ulong)reader.GetInt64(0);
            string cardType = reader.GetString(1);
            string front = reader.GetString(2);
            string back = reader.GetString(3);
            string? answer = reader.IsDBNull(4) ? null : reader.GetString(4);

            cards.Add(cardType switch
            {
                nameof(TypeFlashCard) => new CardExportEntry(cardType, front, back, answer, null, null, null, null, null),
                nameof(FlipFlashCard) => new CardExportEntry(cardType, front, back, null, null, null, null, null, null),
                nameof(MultiFlashCard) => new CardExportEntry(cardType, front, back, null, null, null, null, LoadMultiOptions(connection, cardId), null),
                nameof(MatchFlashCard) => new CardExportEntry(cardType, front, back, null, null, null, null, null, LoadMatchPairs(connection, cardId)),
                nameof(TrueFalseFlashCard) => DeckTransferManager.BuildTrueFalseExportEntry(front, back, answer),
                _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
            });
        }

        return cards;
    }

    public static List<MultiChoiceOptionEntry> LoadMultiOptions(SqliteConnection connection, ulong cardId)
    {
        var options = new List<MultiChoiceOptionEntry>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT OptionText, IsCorrect
            FROM CardOptions
            WHERE CardID = $cardId
            ORDER BY OptionIndex ASC;";
        command.Parameters.AddWithValue("$cardId", cardId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            options.Add(new MultiChoiceOptionEntry(reader.GetString(0), reader.GetInt32(1) == 1));
        }

        return options;
    }

    public static List<MatchPairEntry> LoadMatchPairs(SqliteConnection connection, ulong cardId)
    {
        var pairs = new List<MatchPairEntry>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT LeftText, RightText
            FROM MatchCardPairs
            WHERE CardID = $cardId
            ORDER BY PairIndex ASC;";
        command.Parameters.AddWithValue("$cardId", cardId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
            pairs.Add(new MatchPairEntry(reader.GetString(0), reader.GetString(1)));

        return pairs;
    }

    // --- Write ---

    public static long InsertCard(SqliteConnection connection, SqliteTransaction transaction, long deckId, CardExportEntry card)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO Cards (DeckID, CardType, Front, Back, Answer)
            VALUES ($deckId, $cardType, $front, $back, $answer);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$cardType", card.CardType);
        command.Parameters.AddWithValue("$front", card.Front);
        command.Parameters.AddWithValue("$back", card.Back);
        command.Parameters.AddWithValue("$answer", DeckTransferManager.BuildExportAnswerPayload(card));
        return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert card."));
    }

    public static void InsertMultiChoiceOption(SqliteConnection connection, SqliteTransaction transaction, long cardId, int optionIndex, MultiChoiceOptionEntry option)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO CardOptions (CardID, OptionIndex, OptionText, IsCorrect)
            VALUES ($cardId, $index, $text, $isCorrect);";
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$index", optionIndex);
        command.Parameters.AddWithValue("$text", option.OptionText);
        command.Parameters.AddWithValue("$isCorrect", option.IsCorrect ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public static void InsertMatchPair(SqliteConnection connection, SqliteTransaction transaction, long cardId, int pairIndex, MatchPairEntry pair)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO MatchCardPairs (CardID, PairIndex, LeftText, RightText)
            VALUES ($cardId, $index, $leftText, $rightText);";
        command.Parameters.AddWithValue("$cardId", cardId);
        command.Parameters.AddWithValue("$index", pairIndex);
        command.Parameters.AddWithValue("$leftText", pair.LeftText);
        command.Parameters.AddWithValue("$rightText", pair.RightText);
        command.ExecuteNonQuery();
    }

    public static void InsertDeckStat(SqliteConnection connection, SqliteTransaction transaction, long deckId, DeckStatEntry stat)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO DeckStats (DeckId, CorrectCount, TotalAttempts, TimeTakenSeconds, DateChecked)
            VALUES ($deckId, $correct, $total, $timeTakenSeconds, $dateChecked);";
        command.Parameters.AddWithValue("$deckId", deckId);
        command.Parameters.AddWithValue("$correct", stat.CorrectCount);
        command.Parameters.AddWithValue("$total", stat.TotalAttempts);
        command.Parameters.AddWithValue("$timeTakenSeconds", stat.TimeTakenSeconds);
        command.Parameters.AddWithValue("$dateChecked", stat.DateChecked);
        command.ExecuteNonQuery();
    }

    public static long InsertDeck(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO Decks (Name)
            VALUES ($name);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name);
        return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert deck."));
    }
}