using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using ReviFlash.Data;
using ReviFlash.Models;
using ReviFlash.ViewModels;

public static class BackupManager
{
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

}