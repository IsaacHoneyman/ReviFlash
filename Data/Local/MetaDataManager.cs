using System;
using System.IO;
using System.Text.Json;
using ReviFlash.Models;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;

namespace ReviFlash.Data.Local;

/// <summary> Responsible for meta data mangement in running application. </summary>
public static class MetaDataManager
{
    public static AppMetaData Data { get; private set; } = null!;

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
        data.Version = MainWindowViewModel.VersionText;
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
        catch (Exception ex)
        {
            Logger.LogError("Metadata load failed", ex);
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
        File.WriteAllText(GetFilePath(), JsonSerializer.Serialize(Data, TextUtility.Indented));        
    }

}