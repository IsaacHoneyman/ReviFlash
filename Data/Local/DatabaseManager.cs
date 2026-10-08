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

                CREATE TABLE IF NOT EXISTS Folders (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ParentFolderID INTEGER NULL,
                    FOREIGN KEY (ParentFolderID) REFERENCES Folders(ID) ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS Decks (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    FolderID INTEGER NULL REFERENCES Folders(ID) ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS StudyGroups (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    FolderID INTEGER NULL REFERENCES Folders(ID) ON DELETE SET NULL
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
                    IsReversible INTEGER NOT NULL DEFAULT 0,
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

                -- Lecture notes; SyncID is a stable GUID for syncing.
                CREATE TABLE IF NOT EXISTS Notes (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SyncID TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    FolderID INTEGER NULL REFERENCES Folders(ID) ON DELETE SET NULL,
                    Content TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                -- Time spent in each note per day, like DeckStats for sets.
                CREATE TABLE IF NOT EXISTS NoteStats (
                    NoteID INTEGER NOT NULL,
                    DateStudied DATE NOT NULL DEFAULT (CURRENT_DATE),
                    Seconds INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (NoteID, DateStudied),
                    FOREIGN KEY (NoteID) REFERENCES Notes(ID) ON DELETE CASCADE
                );
            ";
            
            command.ExecuteNonQuery();

            ApplyMigrations(connection);
            CreateIndexes(connection);

            Logger.LogInfo("Database initialisation completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Critical failure during database initialization", ex);
            throw; 
        }
    }

    /// <summary> Brings older databases up to date; every step is guarded, so it is a no-op on a current one. </summary>
    private static void ApplyMigrations(SqliteConnection connection)
    {
        // Folders: adds FolderID to Decks and StudyGroups.
        AddColumnIfMissing(connection, "Decks", "FolderID", "INTEGER NULL REFERENCES Folders(ID) ON DELETE SET NULL");
        AddColumnIfMissing(connection, "StudyGroups", "FolderID", "INTEGER NULL REFERENCES Folders(ID) ON DELETE SET NULL");

        // Reversible flip cards: adds Cards.IsReversible.
        AddColumnIfMissing(connection, "Cards", "IsReversible", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void CreateIndexes(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE INDEX IF NOT EXISTS IX_Folders_Parent ON Folders(ParentFolderID);
            CREATE INDEX IF NOT EXISTS IX_Decks_Folder ON Decks(FolderID);
            CREATE INDEX IF NOT EXISTS IX_StudyGroups_Folder ON StudyGroups(FolderID);
            CREATE INDEX IF NOT EXISTS IX_Cards_Deck ON Cards(DeckID);
            CREATE INDEX IF NOT EXISTS IX_Notes_Folder ON Notes(FolderID);
        ";
        command.ExecuteNonQuery();
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string definition)
    {
        if (ColumnExists(connection, table, column)) return;

        Logger.LogInfo($"Migrating database: adding {table}.{column}.");

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public static SqliteConnection GetConnection() => new(GetConnectionString());
}