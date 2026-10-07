using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using System.Text.Json;
using ReviFlash.Utilities;
using ReviFlash.Data.Local;

namespace ReviFlash.Data.Backup.Local;

/// <summary> Handles local backups and imports. </summary>
public static class BackupManager
{
    // --- Entry ---

    public static void TryCreateBackup(string destinationFolder, bool includeStats = true)
    {
        Logger.LogInfo("Attempting backup creation...");

        if (!Path.IsPathRooted(destinationFolder)) throw new ArgumentException("The backup path must be an absolute root path.");
        Directory.CreateDirectory(destinationFolder);

        var (metadataPath, databasePath) = (TextUtility.MetadataPath, MetaDataManager.Data.DatabasePath);

        if (!File.Exists(metadataPath) || !File.Exists(databasePath))
        {
            Logger.LogError("Backup creation failed.");
            throw new FileNotFoundException($"Cannot create a backup because {TextUtility.MetadataFileName} or {TextUtility.DatabaseFileName} is missing.");
        }

        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"ReviFlashBackup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        string zipFilePath = Path.Combine(destinationFolder, $"ReviFlashBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        // Written under a temporary name, so a failure never leaves a broken backup that looks finished.
        string partialZipPath = zipFilePath + ".partial";

        try
        {
            // The sign-in never goes into a backup: the file may be shared, and restoring an
            // old refresh token could get the live session revoked.
            var metadata = ReadMetadataFromPath(metadataPath);
            metadata.SetSupabase(null, null, null, null, DateTime.MinValue);
            if (!includeStats)
            {
                metadata.LaunchStreak = 0;
                metadata.BestLaunchStreak = 0;
            }
            string stagedMetadata = Path.Combine(stagingDirectory, TextUtility.MetadataFileName);
            File.WriteAllText(stagedMetadata, JsonSerializer.Serialize(metadata, TextUtility.Indented));

            // SQLite's backup API takes a consistent snapshot even if a write is in progress,
            // which copying the file does not.
            string stagedDatabase = Path.Combine(stagingDirectory, TextUtility.DatabaseFileName);
            using (var source = DatabaseManager.GetConnection())
            using (var target = OpenUnpooled(stagedDatabase))
            {
                source.Open();
                source.BackupDatabase(target);
            }
            if (!includeStats) RemoveStatsFromDatabase(stagedDatabase);

            using (var zip = new FileStream(partialZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create))
            {
                AddFileToArchive(archive, stagedMetadata, TextUtility.MetadataFileName);
                AddFileToArchive(archive, stagedDatabase, TextUtility.DatabaseFileName);
                AddTextEntryToArchive(archive, "backup-manifest.json", JsonSerializer.Serialize(new BackupManifest(includeStats)));
            }

            File.Move(partialZipPath, zipFilePath, overwrite: true);
            Logger.LogInfo("Backup complete.");
        }
        catch
        {
            Logger.LogError("Backup creation failed.");
            try { if (File.Exists(partialZipPath)) File.Delete(partialZipPath); }
            catch { Logger.LogError("Partial backup deletion failed."); }
            throw;
        }
        finally
        {
            try { Directory.Delete(stagingDirectory, true); }
            catch { Logger.LogError("Staging directory deletion failed."); }
        }
    }

    public static void TryRestoreBackup(string zipFilePath)
    {
        Logger.LogInfo("Attempting backup restoration...");

        if (!File.Exists(zipFilePath))
        {
            Logger.LogError("Backup restoration failed.");
            throw new FileNotFoundException("The specified backup file does not exist.");
        }

        using var zip = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        bool includeStats = ReadBackupManifest(archive)?.IncludeStats ?? true;

        if (archive.GetEntry(TextUtility.MetadataFileName) == null || archive.GetEntry(TextUtility.DatabaseFileName) == null)
        {
            Logger.LogError("Backup restoration failed.");
            throw new InvalidDataException($"The selected file is not a valid ReviFlash backup. It must contain '{TextUtility.MetadataFileName}' and '{TextUtility.DatabaseFileName}'.");
        }

        string metadataPath = TextUtility.MetadataPath;
        string databasePath = MetaDataManager.Data.DatabasePath;
        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"ReviFlashRestore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        // What is in memory now, to put back if the restore fails part way.
        var previousMetadata = new AppMetaData();
        previousMetadata.ApplyFrom(MetaDataManager.Data);

        try
        {
            string stagedMetadata = ExtractEntryToPath(archive, TextUtility.MetadataFileName, stagingDirectory);
            string stagedDatabase = ExtractEntryToPath(archive, TextUtility.DatabaseFileName, stagingDirectory);

            // Check everything before touching the live files.
            var restoredMetadata = ReadMetadataFromPath(stagedMetadata);
            ValidateDatabase(stagedDatabase);

            BackupExistingFile(metadataPath, stagingDirectory, $"{TextUtility.MetadataFileName}.bak");
            BackupExistingFile(databasePath, stagingDirectory, $"{TextUtility.DatabaseFileName}.bak");

            string? databaseDir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(databaseDir)) Directory.CreateDirectory(databaseDir);

            SqliteConnection.ClearAllPools();
            // A leftover journal belongs to the old database and would be replayed against the new one.
            DeleteJournalFiles(databasePath);
            File.Copy(stagedDatabase, databasePath, overwrite: true);

            DatabaseManager.InitDatabase();
            if (!includeStats)
            {
                MergeRestoredStats(stagingDirectory, databasePath);
                restoredMetadata.LaunchStreak = previousMetadata.LaunchStreak;
                restoredMetadata.BestLaunchStreak = previousMetadata.BestLaunchStreak;
            }

            // Whoever is signed in stays signed in; the backup carries no session.
            restoredMetadata.SetSupabase(
                previousMetadata.SupabaseAccessToken, previousMetadata.SupabaseRefreshToken,
                previousMetadata.SupabaseUserId, previousMetadata.SupabaseUsername, previousMetadata.SupabaseExpirationTime);
            restoredMetadata.DatabasePath = TextUtility.DatabasePath;
            restoredMetadata.Version = TextUtility.VersionText;

            MetaDataManager.LoadMetaDataFrom(restoredMetadata);
            MetaDataManager.SaveMetaData();
            SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
            RefreshOpenViewsAfterRestore();

            Logger.LogInfo("Restore completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Backup restoration failed.");
            TryRestoreRolledBackFiles(stagingDirectory, metadataPath, databasePath);
            MetaDataManager.LoadMetaDataFrom(previousMetadata);
            throw new Exception($"Restore failed: {ex.Message}", ex);
        }
        finally
        {
            try { Directory.Delete(stagingDirectory, true); }
            catch { Logger.LogError("Staging directory deletion failed."); }
        }
    }

    // --- Shared Helpers ---

    /// <summary>
    /// A connection to a staged copy, outside the pool so the file is released (and can be
    /// deleted) as soon as the connection is disposed.
    /// </summary>
    private static SqliteConnection OpenUnpooled(string databasePath, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    // --- Backup Helpers ---

    private static void AddFileToArchive(ZipArchive archive, string sourceFilePath, string entryName)
    {
        var entry = archive.CreateEntry(entryName);

        using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var entryStream = entry.Open();

        fileStream.CopyTo(entryStream);
    }

    private static void AddTextEntryToArchive(ZipArchive archive, string entryName, string contents)
    {
        var entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream);
        writer.Write(contents);
    }

    private static void RemoveStatsFromDatabase(string databasePath)
    {
        using var connection = OpenUnpooled(databasePath);

        using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS DeckStats;";
        command.ExecuteNonQuery();

        command.CommandText = "DROP TABLE IF EXISTS AnswerStreaks;";
        command.ExecuteNonQuery();

        command.CommandText = "DROP TABLE IF EXISTS NoteStats;";
        command.ExecuteNonQuery();
    }

    // --- Restore Helpers ---

    private static BackupManifest? ReadBackupManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("backup-manifest.json");
        if (entry is null) return null;

        using var entryStream = entry.Open();
        return JsonSerializer.Deserialize<BackupManifest>(entryStream);
    }

    /// <summary> Throws unless the file is an intact SQLite database holding a ReviFlash library. </summary>
    private static void ValidateDatabase(string databasePath)
    {
        try
        {
            using var connection = OpenUnpooled(databasePath, SqliteOpenMode.ReadOnly);
            using var command = connection.CreateCommand();

            command.CommandText = "PRAGMA integrity_check;";
            if (command.ExecuteScalar() as string != "ok") throw new InvalidDataException("the database in the backup is damaged.");

            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Decks', 'Cards');";
            if (Convert.ToInt32(command.ExecuteScalar()) != 2) throw new InvalidDataException("the backup does not contain a ReviFlash library.");
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException($"the database in the backup could not be read ({ex.Message}).", ex);
        }
    }

    private static void DeleteJournalFiles(string databasePath)
    {
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            string path = databasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
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
        if (!File.Exists(sourcePath)) return null;

        string backupPath = Path.Combine(destinationDirectory, backupName);
        File.Copy(sourcePath, backupPath, overwrite: true);
        return backupPath;
    }

    private static AppMetaData ReadMetadataFromPath(string metadataPath)
    {
        string json = File.ReadAllText(metadataPath);
        return JsonSerializer.Deserialize<AppMetaData>(json) ?? new AppMetaData();
    }

    private static void TryRestoreRolledBackFiles(string stagingDirectory, string metadataPath, string databasePath)
    {
        try
        {
            string metadataBackupPath = Path.Combine(stagingDirectory, $"{TextUtility.MetadataFileName}.bak");
            string databaseBackupPath = Path.Combine(stagingDirectory, $"{TextUtility.DatabaseFileName}.bak");

            SqliteConnection.ClearAllPools();

            if (File.Exists(metadataBackupPath))
                File.Copy(metadataBackupPath, metadataPath, overwrite: true);

            if (File.Exists(databaseBackupPath))
            {
                DeleteJournalFiles(databasePath);
                File.Copy(databaseBackupPath, databasePath, overwrite: true);
            }

            DatabaseManager.InitDatabase();
        }
        catch { Logger.LogInfo("Rollback failed."); }
    }

    private static void MergeRestoredStats(string sourceDirectory, string targetDatabasePath)
    {
        string sourceDatabasePath = Path.Combine(sourceDirectory, $"{TextUtility.DatabaseFileName}.bak");
        if (!File.Exists(sourceDatabasePath)) return;

        // Only stats for decks the backup has: the rest have nothing to attach to (and the
        // foreign key would reject them).
        RestoreTableFromBackup(sourceDatabasePath, targetDatabasePath, "DeckStats", ["DeckId", "CorrectCount", "TotalAttempts", "TimeTakenSeconds", "DateChecked"],
            where: "DeckId IN (SELECT ID FROM main.Decks)");
        RestoreTableFromBackup(sourceDatabasePath, targetDatabasePath, "AnswerStreaks", ["TargetType", "TargetId", "BestStreak"]);
        RestoreTableFromBackup(sourceDatabasePath, targetDatabasePath, "NoteStats", ["NoteID", "DateStudied", "Seconds"],
            where: "NoteID IN (SELECT ID FROM main.Notes)");
    }

    private static void RestoreTableFromBackup(string sourceDatabasePath, string targetDatabasePath, string tableName, IReadOnlyList<string> columns, string? where = null)
    {
        using var connection = OpenUnpooled(targetDatabasePath);

        using (var attach = connection.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE $source AS source;";
            attach.Parameters.AddWithValue("$source", sourceDatabasePath);
            attach.ExecuteNonQuery();
        }

        using (var transaction = connection.BeginTransaction())
        using (var command = connection.CreateCommand())
        {
            string columnList = string.Join(", ", columns);
            command.Transaction = transaction;
            command.CommandText = $"""
                DELETE FROM main.{tableName};
                INSERT INTO main.{tableName} ({columnList})
                SELECT {columnList} FROM source.{tableName}{(where is null ? "" : $" WHERE {where}")};
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        using var detach = connection.CreateCommand();
        detach.CommandText = "DETACH DATABASE source;";
        detach.ExecuteNonQuery();
    }

    private static void RefreshOpenViewsAfterRestore()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;

        if (desktop.MainWindow?.DataContext is DashboardViewModel dashboardViewModel)
            dashboardViewModel.RefreshAfterBackupRestore();

        foreach (var window in desktop.Windows)
            if (window.DataContext is SettingsViewModel settingsViewModel) settingsViewModel.RefreshFromMetadata();
    }
}
