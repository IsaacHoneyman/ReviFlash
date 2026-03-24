using System;
using System.IO;
using System.Text.Json;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Data;

public static class MetaDataManager
{
    private const string filePath = "metadata.json";

    public static AppMetaData LoadMetaDataOnStartup()
    {
        AppMetaData data = LoadMetaData();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        
        if (today == data.LastLaunchDate.AddDays(1))
        {
            data.LaunchStreak++;
        }
        else if (today != data.LastLaunchDate)
        {
            data.LaunchStreak = 1;
        }

        data.LastLaunchDate = DateOnly.FromDateTime(DateTime.Now);
        SettingsViewModel.ApplyTheme(data.Theme);
        data.Version = MainWindowViewModel.VersionText;
        SaveMetaData(data);
        return data;
    }

    static AppMetaData LoadMetaData()
    {
        if (!File.Exists(filePath))
        {
            var defaultMetaData = new AppMetaData();
            return defaultMetaData;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppMetaData>(json) ?? new AppMetaData();
        }
        catch (Exception)
        {
            return new AppMetaData();
        }
    }

    public static void SaveMetaData(AppMetaData data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }

}