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
                FOREIGN KEY(DeckID) REFERENCES Decks(ID) ON DELETE CASCADE
            )
        ";
        cardCommand.ExecuteNonQuery();

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
}