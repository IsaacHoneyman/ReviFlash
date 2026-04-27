using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using ReviFlash.Data;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using System.Text.Json;

public static class BackupManager
{
    private sealed record TrueFalseAnswerPayload(bool CorrectAnswerIsTrue, string TrueLabel, string FalseLabel);

    private sealed record FlashCardExportPackage(List<DeckExportEntry> Decks);

    private sealed record DeckExportEntry(
        string Name,
        List<CardExportEntry> Cards,
        List<DeckStatEntry> Stats);

    private sealed record CardExportEntry(
        string CardType,
        string Front,
        string Back,
        string? Answer,
        bool? CorrectAnswerIsTrue,
        string? TrueLabel,
        string? FalseLabel,
        List<MultiChoiceOptionEntry>? Options,
        List<MatchPairEntry>? Pairs);

    private sealed record MultiChoiceOptionEntry(string OptionText, bool IsCorrect);

    private sealed record MatchPairEntry(string LeftText, string RightText);

    private sealed record DeckStatEntry(int CorrectCount, int TotalAttempts, int TimeTakenSeconds, string DateChecked);

    public static void TryCreateBackup(string destinationFolder)
    {
        if (!Path.IsPathRooted(destinationFolder))
        {
            throw new ArgumentException("The backup path must be an absolute root path.");
        }

        Directory.CreateDirectory(destinationFolder);

        string zipFilePath = Path.Combine(destinationFolder, $"ReviFlashBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        using var zip = new FileStream(zipFilePath, FileMode.Create);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Create);

        AddFileToArchiveSafely(archive, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "metadata.json"));
        AddFileToArchiveSafely(archive, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reviflash.db"));
    }

    private static void AddFileToArchiveSafely(ZipArchive archive, string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            return;
        }

        string fileName = Path.GetFileName(sourceFilePath);
        var entry = archive.CreateEntry(fileName);

        using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var entryStream = entry.Open();

        fileStream.CopyTo(entryStream);
    }

    public static void TryRestoreFromBackup(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("The specified backup file does not exist.");
        }

        using var zip = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        if (archive.GetEntry("metadata.json") == null ||
            archive.GetEntry("reviflash.db") == null)
        {
            throw new InvalidDataException("The selected file is not a valid ReviFlash backup. It must contain 'metadata.json' and 'reviflash.db'.");
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string metadataPath = Path.Combine(baseDirectory, "metadata.json");
        string databasePath = Path.Combine(baseDirectory, "reviflash.db");
        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"ReviFlashRestore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            string stagedMetadata = ExtractEntryToPath(archive, "metadata.json", stagingDirectory);
            string stagedDatabase = ExtractEntryToPath(archive, "reviflash.db", stagingDirectory);

            BackupExistingFile(metadataPath, stagingDirectory, "metadata.json.bak");
            BackupExistingFile(databasePath, stagingDirectory, "reviflash.db.bak");

            SqliteConnection.ClearAllPools();

            File.Copy(stagedMetadata, metadataPath, overwrite: true);
            File.Copy(stagedDatabase, databasePath, overwrite: true);

            DatabaseManager.InitDatabase();
            ApplyRestoredMetadata(metadataPath);
            RefreshOpenViewsAfterRestore();

            Console.WriteLine("Restore completed successfully!");
        }
        catch (Exception ex)
        {
            TryRestoreRolledBackFiles(stagingDirectory, metadataPath, databasePath);
            throw new Exception($"Restore failed: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                Directory.Delete(stagingDirectory, true);
            }
            catch
            {
            }
        }
    }

    public static void TryCreateDeckExport(string destinationFilePath, IReadOnlyCollection<ulong> deckIds)
    {
        if (deckIds.Count == 0)
        {
            throw new ArgumentException("At least one deck must be selected for export.");
        }

        var exportData = BuildExportPackage(deckIds.Distinct().ToList());
        if (exportData.Decks.Count == 0)
        {
            throw new InvalidOperationException("None of the selected decks could be exported.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var zip = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("flashcards.json");
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int TryImportDeckExport(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("The specified export file does not exist.");
        }

        using var zip = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        var exportEntry = archive.GetEntry("flashcards.json");
        if (exportEntry is null)
        {
            throw new InvalidDataException("The selected file is not a valid ReviFlash flashcard export.");
        }

        using var entryStream = exportEntry.Open();
        var package = JsonSerializer.Deserialize<FlashCardExportPackage>(entryStream)
            ?? throw new InvalidDataException("The export file could not be read.");

        if (package.Decks.Count == 0)
        {
            throw new InvalidDataException("The export file does not contain any decks.");
        }

        DatabaseManager.InitDatabase();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        int importedDeckCount = 0;
        foreach (var deck in package.Decks)
        {
            long deckId = InsertDeck(connection, transaction, deck.Name);

            foreach (var card in deck.Cards)
            {
                long cardId = InsertCard(connection, transaction, deckId, card);

                if (card.Options is not null)
                {
                    foreach (var (index, option) in card.Options.Select((value, index) => (index, value)))
                    {
                        InsertMultiChoiceOption(connection, transaction, cardId, index, option);
                    }
                }

                if (card.Pairs is not null)
                {
                    foreach (var (index, pair) in card.Pairs.Select((value, index) => (index, value)))
                    {
                        InsertMatchPair(connection, transaction, cardId, index, pair);
                    }
                }
            }

            foreach (var stat in deck.Stats)
            {
                InsertDeckStat(connection, transaction, deckId, stat);
            }

            importedDeckCount++;
        }

        transaction.Commit();
        return importedDeckCount;
    }

    private static string ExtractEntryToPath(ZipArchive archive, string entryName, string destinationDirectory)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Missing backup entry: {entryName}");
        string destinationPath = Path.Combine(destinationDirectory, entryName);
        entry.ExtractToFile(destinationPath, overwrite: true);
        return destinationPath;
    }

    private static string? BackupExistingFile(string sourcePath, string destinationDirectory, string backupName)
    {
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        string backupPath = Path.Combine(destinationDirectory, backupName);
        File.Copy(sourcePath, backupPath, overwrite: true);
        return backupPath;
    }

    private static void TryRestoreRolledBackFiles(string stagingDirectory, string metadataPath, string databasePath)
    {
        try
        {
            string metadataBackupPath = Path.Combine(stagingDirectory, "metadata.json.bak");
            string databaseBackupPath = Path.Combine(stagingDirectory, "reviflash.db.bak");

            if (File.Exists(metadataBackupPath))
            {
                File.Copy(metadataBackupPath, metadataPath, overwrite: true);
            }

            if (File.Exists(databaseBackupPath))
            {
                File.Copy(databaseBackupPath, databasePath, overwrite: true);
            }

            DatabaseManager.InitDatabase();
        }
        catch
        {
        }
    }

    private static void ApplyRestoredMetadata(string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(metadataPath);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<AppMetaData>(json);
            if (metadata is null)
            {
                return;
            }

            ReviFlash.App.SetCurrentMetaData(metadata);
            SettingsViewModel.ApplyTheme(metadata.Theme);
        }
        catch
        {
        }
    }

    private static void RefreshOpenViewsAfterRestore()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow?.DataContext is MainWindowViewModel mainWindowViewModel)
        {
            mainWindowViewModel.RefreshAfterBackupRestore();
        }

        foreach (var window in desktop.Windows)
        {
            if (window.DataContext is SettingsViewModel settingsViewModel)
            {
                settingsViewModel.RefreshFromMetadata();
            }
        }
    }

    private static FlashCardExportPackage BuildExportPackage(IReadOnlyCollection<ulong> deckIds)
    {
        var decks = new List<DeckExportEntry>();

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        foreach (var deckId in deckIds)
        {
            var deckName = GetDeckName(connection, deckId);
            if (deckName is null)
            {
                continue;
            }

            decks.Add(new DeckExportEntry(
                deckName,
                LoadDeckCards(connection, deckId),
                LoadDeckStats(connection, deckId)));
        }

        return new FlashCardExportPackage(decks);
    }

    private static string? GetDeckName(SqliteConnection connection, ulong deckId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Decks WHERE ID = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);

        var result = command.ExecuteScalar();
        return result as string;
    }

    private static List<CardExportEntry> LoadDeckCards(SqliteConnection connection, ulong deckId)
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
                nameof(TrueFalseFlashCard) => BuildTrueFalseExportEntry(front, back, answer),
                _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
            });
        }

        return cards;
    }

    private static CardExportEntry BuildTrueFalseExportEntry(string front, string back, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);
        }

        if (bool.TryParse(answer, out var parsedBool))
        {
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, parsedBool, "True", "False", null, null);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<TrueFalseAnswerPayload>(answer);
            if (payload is null)
            {
                return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);
            }

            return new CardExportEntry(
                nameof(TrueFalseFlashCard),
                front,
                back,
                null,
                payload.CorrectAnswerIsTrue,
                payload.TrueLabel,
                payload.FalseLabel,
                null,
                null);
        }
        catch (JsonException)
        {
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);
        }
    }

    private static List<MultiChoiceOptionEntry> LoadMultiOptions(SqliteConnection connection, ulong cardId)
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

    private static List<MatchPairEntry> LoadMatchPairs(SqliteConnection connection, ulong cardId)
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
        {
            pairs.Add(new MatchPairEntry(reader.GetString(0), reader.GetString(1)));
        }

        return pairs;
    }

    private static List<DeckStatEntry> LoadDeckStats(SqliteConnection connection, ulong deckId)
    {
        var stats = new List<DeckStatEntry>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT CorrectCount, TotalAttempts, TimeTakenSeconds, DateChecked
            FROM DeckStats
            WHERE DeckId = $deckId
            ORDER BY DateChecked ASC;";
        command.Parameters.AddWithValue("$deckId", deckId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            stats.Add(new DeckStatEntry(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3)));
        }

        return stats;
    }

    private static long InsertDeck(SqliteConnection connection, SqliteTransaction transaction, string name)
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

    private static long InsertCard(SqliteConnection connection, SqliteTransaction transaction, long deckId, CardExportEntry card)
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
        command.Parameters.AddWithValue("$answer", BuildExportAnswerPayload(card));
        return (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Failed to insert card."));
    }

    private static object BuildExportAnswerPayload(CardExportEntry card)
    {
        return card.CardType switch
        {
            nameof(TypeFlashCard) => card.Answer ?? card.Back,
            nameof(TrueFalseFlashCard) => JsonSerializer.Serialize(new TrueFalseAnswerPayload(
                card.CorrectAnswerIsTrue ?? true,
                string.IsNullOrWhiteSpace(card.TrueLabel) ? "True" : card.TrueLabel!,
                string.IsNullOrWhiteSpace(card.FalseLabel) ? "False" : card.FalseLabel!)),
            _ => DBNull.Value,
        };
    }

    private static void InsertMultiChoiceOption(SqliteConnection connection, SqliteTransaction transaction, long cardId, int optionIndex, MultiChoiceOptionEntry option)
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

    private static void InsertMatchPair(SqliteConnection connection, SqliteTransaction transaction, long cardId, int pairIndex, MatchPairEntry pair)
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

    private static void InsertDeckStat(SqliteConnection connection, SqliteTransaction transaction, long deckId, DeckStatEntry stat)
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

}