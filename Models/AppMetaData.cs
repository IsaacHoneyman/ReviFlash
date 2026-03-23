using System;

namespace ReviFlash.Models;

public class AppMetaData
{
    public string Theme { get; set;} = "Dark";

    public DateOnly FirstLaunchDate { get; set;} = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly LastLaunchDate { get; set;} = DateOnly.FromDateTime(DateTime.Now);
    public int LaunchStreak { get; set;} = 1;
}