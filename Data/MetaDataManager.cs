using System;
using System.IO;
using System.Text.Json;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Data;

public static class MetaDataManager
{
    private static string GetFilePath()
    {
        return AppStoragePaths.MetadataPath;
    }

    public static AppMetaData LoadMetaDataOnStartup()
    {
        AppMetaData data = LoadMetaData();
        if (data.Theme == "Dark")
        {
            data.Theme = "Default";
        }
        else if (data.Theme == "Pastel")
        {
            data.Theme = "Plains";
        }

        if (string.IsNullOrWhiteSpace(data.DatabasePath))
        {
            data.DatabasePath = AppStoragePaths.DatabasePath;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        
        if (today == data.LastLaunchDate.AddDays(1))
        {
            data.LaunchStreak++;
        }
        else if (today != data.LastLaunchDate)
        {
            data.LaunchStreak = 1;
        }

        data.BestLaunchStreak = Math.Max(data.BestLaunchStreak, data.LaunchStreak);

        data.LastLaunchDate = DateOnly.FromDateTime(DateTime.Now);
        SettingsViewModel.ApplyTheme(data, data.Theme);
        data.Version = MainWindowViewModel.VersionText;
        SaveMetaData(data);
        return data;
    }

    static AppMetaData LoadMetaData()
    {
        if (!File.Exists(GetFilePath()))
        {
            var defaultMetaData = new AppMetaData();
            return defaultMetaData;
        }

        try
        {
            string json = File.ReadAllText(GetFilePath());
            return JsonSerializer.Deserialize<AppMetaData>(json) ?? new AppMetaData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load metadata", ex);
            return new AppMetaData();
        }
    }

    public static void SaveMetaData(AppMetaData data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(GetFilePath(), json);
    }

}