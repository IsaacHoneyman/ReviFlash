using System;
using System.IO;
using System.Text.Json;
using ReviFlash.Models;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;

namespace ReviFlash.Data.Local;

public static class MetaDataManager
{
    public static AppMetaData Data { get; private set; } = null!;

    /// <summary> The version that last ran, e.g. "Version 1.3.3"; the current one on a fresh install. </summary>
    public static string VersionBeforeLaunch { get; private set; } = "";

    private static string GetFilePath()
    {
        return TextUtility.MetadataPath;
    }

    public static void InitMetaData()
    {
        AppMetaData data = LoadMetaData();

        if (string.IsNullOrWhiteSpace(data.DatabasePath)) data.DatabasePath = TextUtility.DatabasePath;

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        if (today == data.LastLaunchDate.AddDays(1)) data.LaunchStreak++;
        else if (today != data.LastLaunchDate) data.LaunchStreak = 1;

        data.BestLaunchStreak = Math.Max(data.BestLaunchStreak, data.LaunchStreak);
        data.LastLaunchDate = DateOnly.FromDateTime(DateTime.Now);

        SettingsViewModel.ApplyTheme(data, data.Theme);
        VersionBeforeLaunch = data.Version;
        data.Version = TextUtility.VersionText;
        Data = data;
        SaveMetaData();

        Logger.LogInfo("ReviFlash metadata initalised.");
    }

    public static void LoadMetaDataFrom(AppMetaData data)
    {
        Data.ApplyFrom(data);
    }

    static AppMetaData LoadMetaData()
    {
        if (!File.Exists(GetFilePath())) _ = new AppMetaData();

        try
        {
            string json = File.ReadAllText(GetFilePath());
            bool legacySkipRedoButtonsDisabled = false;

            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.TryGetProperty("ShowSkipRedoButtons", out var legacySkipProperty)
                    && legacySkipProperty.ValueKind == JsonValueKind.False)
                {
                    legacySkipRedoButtonsDisabled = true;
                }
            }

            var data = JsonSerializer.Deserialize<AppMetaData>(json) ?? new AppMetaData();
            if (legacySkipRedoButtonsDisabled)
            {
                data.ShowSkipButton = false;
                data.ShowRetryLaterButton = false;
            }

            return data;
        }
        catch (Exception)
        {
            Logger.LogWarning("Metadata load failed");
            return new();
        }
    }

    public static void SaveMetaData()
    {
        if (Data == null)
        {
            Logger.LogWarning("Meta Data attempted to save NULL instance.");
            return;
        }

        var path = GetFilePath();
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Data, TextUtility.Indented));
        File.Move(temporaryPath, path, overwrite: true);
    }

}