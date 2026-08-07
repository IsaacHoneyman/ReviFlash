using System;
using Microsoft.Data.Sqlite;

namespace ReviFlash.Data.Local;

public static class DatabaseManager
{
    private static string GetConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = MetaDataManager.Data.DatabasePath,
            ForeignKeys = true
        };

        return builder.ToString();
    }

    public static void InitDatabase()
    {
        try
        {
            Logger.LogInfo("Initializing ReviFlash local database...");
            
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            
            command.CommandText = @"
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Decks (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS StudyGroups (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS StudyGroupDecks (
                    StudyGroupID INTEGER NOT NULL,
                    DeckID INTEGER NOT NULL,
                    PRIMARY KEY (StudyGroupID, DeckID),
                    FOREIGN KEY (StudyGroupID) REFERENCES StudyGroups(ID) ON DELETE CASCADE,
                    FOREIGN KEY (DeckID) REFERENCES Decks(ID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Cards (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeckID INTEGER NOT NULL,
                    CardType TEXT NOT NULL, 
                    Front TEXT NOT NULL,
                    Back TEXT NOT NULL,
                    Answer TEXT,
                    FOREIGN KEY(DeckID) REFERENCES Decks(ID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS CardOptions (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    OptionIndex INTEGER NOT NULL,
                    OptionText TEXT NOT NULL,
                    IsCorrect INTEGER NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Cards(ID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS MatchCardPairs (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    PairIndex INTEGER NOT NULL,
                    LeftText TEXT NOT NULL,
                    RightText TEXT NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Cards(ID) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS DeckStats (
                    DeckId INTEGER NOT NULL,
                    CorrectCount INTEGER DEFAULT 0,
                    TotalAttempts INTEGER DEFAULT 0,
                    TimeTakenSeconds INTEGER DEFAULT 0,
                    DateChecked DATE DEFAULT (CURRENT_DATE), 
                    PRIMARY KEY (DeckId, DateChecked),
                    FOREIGN KEY (DeckId) REFERENCES Decks(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS AnswerStreaks (
                    TargetType TEXT NOT NULL,
                    TargetId INTEGER NOT NULL,
                    BestStreak INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (TargetType, TargetId)
                );
            ";
            
            command.ExecuteNonQuery();
            Logger.LogInfo("Database initialisation completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Critical failure during database initialization", ex);
            throw; 
        }
    }

    public static SqliteConnection GetConnection() => new(GetConnectionString());
}