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
using System.Reflection.Metadata.Ecma335;

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

        string? tempDatabasePath = null;
        string? tempMetadataPath = null;
        string databaseBackupPath = databasePath;
        string metadataBackupPath = metadataPath;

        try
        {
            if (!includeStats)
            {
                tempMetadataPath = Path.Combine(Path.GetTempPath(), $"ReviFlashBackup_{Guid.NewGuid():N}.json");
                File.Copy(metadataPath, tempMetadataPath, overwrite: true);
                StripStatsFromMetadata(tempMetadataPath);
                metadataBackupPath = tempMetadataPath;

                tempDatabasePath = Path.Combine(Path.GetTempPath(), $"ReviFlashBackup_{Guid.NewGuid():N}.db");
                File.Copy(databasePath, tempDatabasePath, overwrite: true);
                RemoveStatsFromDatabase(tempDatabasePath);
                databaseBackupPath = tempDatabasePath;
            }

            string zipFilePath = Path.Combine(destinationFolder, $"ReviFlashBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            using var zip = new FileStream(zipFilePath, FileMode.Create);
            using var archive = new ZipArchive(zip, ZipArchiveMode.Create);

            AddFileToArchive(archive, metadataBackupPath, TextUtility.MetadataFileName);
            AddFileToArchive(archive, databaseBackupPath, TextUtility.DatabaseFileName);
            AddTextEntryToArchive(archive, "backup-manifest.json", JsonSerializer.Serialize(new BackupManifest(includeStats)));
            Logger.LogInfo("Backup complete.");
        }
        finally
        {
            if (tempMetadataPath is not null)
            {
                try { if (File.Exists(tempMetadataPath)) { File.Delete(tempMetadataPath); } }
                catch { Logger.LogError("Staging directory deletion failed."); }
            }

            if (tempDatabasePath is not null)
            {
                try { if (File.Exists(tempDatabasePath)) File.Delete(tempDatabasePath); }
                catch { Logger.LogError("Staging directory deletion failed."); }
            }
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

        try
        {
            string stagedMetadata = ExtractEntryToPath(archive, TextUtility.MetadataFileName, stagingDirectory);
            string stagedDatabase = ExtractEntryToPath(archive, TextUtility.DatabaseFileName, stagingDirectory);

            var restoredMetadata = ReadMetadataFromPath(stagedMetadata);
            AppMetaData? currentMetadata = File.Exists(metadataPath) ? ReadMetadataFromPath(metadataPath) : null;
            databasePath = MetaDataManager.Data.DatabasePath;

            BackupExistingFile(metadataPath, stagingDirectory, $"{TextUtility.MetadataFileName}.bak");
            BackupExistingFile(databasePath, stagingDirectory, $"{TextUtility.DatabaseFileName}.bak");

            string? metadataDir = Path.GetDirectoryName(metadataPath);
            string? databaseDir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(metadataDir))
            {
                Directory.CreateDirectory(metadataDir);
            }
            if (!string.IsNullOrWhiteSpace(databaseDir))
            {
                Directory.CreateDirectory(databaseDir);
            }

            SqliteConnection.ClearAllPools();

            File.Copy(stagedMetadata, metadataPath, overwrite: true);
            File.Copy(stagedDatabase, databasePath, overwrite: true);

            DatabaseManager.InitDatabase();
            if (!includeStats)
            {
                MergeRestoredStats(stagingDirectory, databasePath);

                if (currentMetadata is not null)
                {
                    restoredMetadata.LaunchStreak = currentMetadata.LaunchStreak;
                    restoredMetadata.BestLaunchStreak = currentMetadata.BestLaunchStreak;
                }
            }

            MetaDataManager.Data.DatabasePath = TextUtility.DatabasePath;
            MetaDataManager.LoadMetaDataFrom(MetaDataManager.Data);
            SettingsViewModel.ApplyTheme(MetaDataManager.Data, MetaDataManager.Data.Theme);
            RefreshOpenViewsAfterRestore();

            Logger.LogInfo("Restore completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Backup restoration failed.");
            TryRestoreRolledBackFiles(stagingDirectory, metadataPath, databasePath);
            throw new Exception($"Restore failed: {ex.Message}", ex);
        }
        finally
        {
            try { Directory.Delete(stagingDirectory, true); }
            catch { Logger.LogError("Staging directory deletion failed."); }
        }
    }

    // --- Backup Helpers ---

    private static bool AddFileToArchive(ZipArchive archive, string sourceFilePath, string entryName)
    {
        if (!File.Exists(sourceFilePath))
        {
            Logger.LogError($"Skipping missing backup file: {sourceFilePath}");
            return false;
        }

        var entry = archive.CreateEntry(entryName);

        using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var entryStream = entry.Open();

        fileStream.CopyTo(entryStream);
        return true;
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
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS DeckStats;";
        command.ExecuteNonQuery();

        command.CommandText = "DROP TABLE IF EXISTS AnswerStreaks;";
        command.ExecuteNonQuery();
    }

    private static void StripStatsFromMetadata(string metadataPath)
    {
        var metadata = ReadMetadataFromPath(metadataPath);
        metadata.LaunchStreak = 0;
        metadata.BestLaunchStreak = 0;
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, TextUtility.Indented));
    }

    // --- Restore Helpers ---

    private static BackupManifest? ReadBackupManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("backup-manifest.json");
        if (entry is null) return null;

        using var entryStream = entry.Open();
        return JsonSerializer.Deserialize<BackupManifest>(entryStream);
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

            if (File.Exists(metadataBackupPath))
                File.Copy(metadataBackupPath, metadataPath, overwrite: true);

            if (File.Exists(databaseBackupPath))
                File.Copy(databaseBackupPath, databasePath, overwrite: true);

            DatabaseManager.InitDatabase();
        }
        catch { Logger.LogInfo("Rollback failed."); }
    }

    private static void MergeRestoredStats(string sourceDirectory, string targetDatabasePath)
    {
        string sourceDatabasePath = Path.Combine(sourceDirectory, $"{TextUtility.DatabaseFileName}.bak");
        if (!File.Exists(sourceDatabasePath)) return;

        RestoreTableFromBackup(sourceDatabasePath, targetDatabasePath, "DeckStats", ["DeckId", "CorrectCount", "TotalAttempts", "TimeTakenSeconds", "DateChecked"]);
        RestoreTableFromBackup(sourceDatabasePath, targetDatabasePath, "AnswerStreaks", ["TargetType", "TargetId", "BestStreak"]);
    }

    private static void RestoreTableFromBackup(string sourceDatabasePath, string targetDatabasePath, string tableName, IReadOnlyList<string> columns)
    {
        using var sourceConnection = new SqliteConnection($"Data Source={sourceDatabasePath}");
        using var targetConnection = new SqliteConnection($"Data Source={targetDatabasePath}");
        sourceConnection.Open();
        targetConnection.Open();

        using var transaction = targetConnection.BeginTransaction();

        using (var clearCommand = targetConnection.CreateCommand())
        {
            clearCommand.Transaction = transaction;
            clearCommand.CommandText = $"DELETE FROM {tableName};";
            clearCommand.ExecuteNonQuery();
        }

        using var selectCommand = sourceConnection.CreateCommand();
        selectCommand.CommandText = $"SELECT {string.Join(", ", columns)} FROM {tableName};";

        using var reader = selectCommand.ExecuteReader();
        while (reader.Read())
        {
            using var insertCommand = targetConnection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", columns.Select(column => "$" + column))});";

            for (int i = 0; i < columns.Count; i++)
            {
                object value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                insertCommand.Parameters.AddWithValue("$" + columns[i], value);
            }

            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void RefreshOpenViewsAfterRestore()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;

        if (desktop.MainWindow?.DataContext is MainWindowViewModel mainWindowViewModel)
            mainWindowViewModel.RefreshAfterBackupRestore();

        foreach (var window in desktop.Windows)
            if (window.DataContext is SettingsViewModel settingsViewModel) settingsViewModel.RefreshFromMetadata();
    }
}