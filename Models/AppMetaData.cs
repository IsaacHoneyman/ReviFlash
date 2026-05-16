using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReviFlash.Data;
using ReviFlash.ViewModels;

namespace ReviFlash.Models;

public class AppMetaData : INotifyPropertyChanged
{
    private string _theme = "Default";
    private DateOnly _firstLaunchDate = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly _lastLaunchDate = DateOnly.FromDateTime(DateTime.Now);
    private int _launchStreak = 1;
    private int _bestLaunchStreak = 1;
    private string _version = MainWindowViewModel.VersionText;
    private bool _showTimer = true;
    private bool _showProgress = true;
    private bool _showAdditionalFieldLatexPreviews = true;
    private bool _showBackgroundSwirl = true;
    private string _databasePath = AppStoragePaths.DatabasePath;

    public string Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public DateOnly FirstLaunchDate
    {
        get => _firstLaunchDate;
        set => SetField(ref _firstLaunchDate, value);
    }

    public DateOnly LastLaunchDate
    {
        get => _lastLaunchDate;
        set => SetField(ref _lastLaunchDate, value);
    }

    public int LaunchStreak
    {
        get => _launchStreak;
        set => SetField(ref _launchStreak, value);
    }

    public int BestLaunchStreak
    {
        get => _bestLaunchStreak;
        set => SetField(ref _bestLaunchStreak, value);
    }

    public string Version
    {
        get => _version;
        set => SetField(ref _version, value);
    }

    public bool ShowTimer
    {
        get => _showTimer;
        set => SetField(ref _showTimer, value);
    }

    public bool ShowProgress
    {
        get => _showProgress;
        set => SetField(ref _showProgress, value);
    }

    public bool ShowAdditionalFieldLatexPreviews
    {
        get => _showAdditionalFieldLatexPreviews;
        set => SetField(ref _showAdditionalFieldLatexPreviews, value);
    }

    public bool ShowBackgroundSwirl
    {
        get => _showBackgroundSwirl;
        set => SetField(ref _showBackgroundSwirl, value);
    }

    public string DatabasePath
    {
        get => _databasePath;
        set => SetField(ref _databasePath, string.IsNullOrWhiteSpace(value) ? AppStoragePaths.DatabasePath : value);
    }

    public AppMetaData()
    {
        Version = MainWindowViewModel.VersionText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public void ApplyFrom(AppMetaData other)
    {
        Theme = other.Theme;
        FirstLaunchDate = other.FirstLaunchDate;
        LastLaunchDate = other.LastLaunchDate;
        LaunchStreak = other.LaunchStreak;
        BestLaunchStreak = other.BestLaunchStreak;
        Version = other.Version;
        ShowTimer = other.ShowTimer;
        ShowProgress = other.ShowProgress;
        ShowAdditionalFieldLatexPreviews = other.ShowAdditionalFieldLatexPreviews;
        ShowBackgroundSwirl = other.ShowBackgroundSwirl;
        DatabasePath = other.DatabasePath;
    }
}