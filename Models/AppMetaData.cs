using System;
using ReviFlash.ViewModels;
using ReviFlash.Views;

namespace ReviFlash.Models;

public class AppMetaData
{
    public string Theme { get; set;} = "Default";

    public DateOnly FirstLaunchDate { get; set;} = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly LastLaunchDate { get; set;} = DateOnly.FromDateTime(DateTime.Now);
    public int LaunchStreak { get; set;} = 1;
    public int BestLaunchStreak { get; set;} = 1;
    public string Version { get; set; }
    public bool ShowTimer { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowAdditionalFieldLatexPreviews { get; set; } = true;
    public bool ShowBackgroundSwirl { get; set; } = true;

    public AppMetaData()
    {
        Version = MainWindowViewModel.VersionText;
    }
}