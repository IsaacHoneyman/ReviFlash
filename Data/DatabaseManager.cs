using System;
using Microsoft.Data.Sqlite;

namespace ReviFlash.Data;

public static class DatabaseManager
{
    private const string connectionString = "Data Source=reviflash.db";

    public static void InitDatabase()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var deckCommand = connection.CreateCommand();
        deckCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS Decks (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )
        ";
        deckCommand.ExecuteNonQuery();

        var cardCommand = connection.CreateCommand();
        cardCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS Cards (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                DeckID INTEGER NOT NULL,
                CardType TEXT NOT NULL, 
                Front TEXT NOT NULL,
                Back TEXT NOT NULL,
                Answer TEXT,
                FOREIGN KEY(DeckID) REFERENCES Decks(ID) ON DELETE CASCADE
            )
        ";
        cardCommand.ExecuteNonQuery();

        EnsureCardsAnswerColumn(connection);

        var cardOptionsCommand = connection.CreateCommand();
        cardOptionsCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS CardOptions (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                CardID INTEGER NOT NULL,
                OptionIndex INTEGER NOT NULL,
                OptionText TEXT NOT NULL,
                IsCorrect INTEGER NOT NULL,
                FOREIGN KEY(CardID) REFERENCES Cards(ID) ON DELETE CASCADE
            )
        ";
        cardOptionsCommand.ExecuteNonQuery();

        var matchPairsCommand = connection.CreateCommand();
        matchPairsCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS MatchCardPairs (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                CardID INTEGER NOT NULL,
                PairIndex INTEGER NOT NULL,
                LeftText TEXT NOT NULL,
                RightText TEXT NOT NULL,
                FOREIGN KEY(CardID) REFERENCES Cards(ID) ON DELETE CASCADE
            )
        ";
        matchPairsCommand.ExecuteNonQuery();

        var deckStatsCommand = connection.CreateCommand();
        deckStatsCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS DeckStats (
            DeckId INTEGER NOT NULL,
            CorrectCount INTEGER DEFAULT 0,
            TotalAttempts INTEGER DEFAULT 0,
            TimeTakenSeconds INTEGER DEFAULT 0,
            DateChecked DATE DEFAULT (CURRENT_DATE), 
            PRIMARY KEY (DeckId, DateChecked),
            FOREIGN KEY (DeckId) REFERENCES Decks(Id) ON DELETE CASCADE
        )";
        deckStatsCommand.ExecuteNonQuery(); 
    }

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection(connectionString);
    }

    private static void EnsureCardsAnswerColumn(SqliteConnection connection)
    {
        var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(Cards);";

        var hasAnswerColumn = false;
        using (var reader = pragmaCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "Answer", StringComparison.OrdinalIgnoreCase))
                {
                    hasAnswerColumn = true;
                    break;
                }
            }
        }

        if (!hasAnswerColumn)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Cards ADD COLUMN Answer TEXT;";
            alterCommand.ExecuteNonQuery();
        }
    }
}