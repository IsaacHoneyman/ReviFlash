using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Data;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;

namespace ReviFlash.Models;

/// <summary> Non flash card persistent data. </summary>
public partial class AppMetaData : ObservableObject
{
    [ObservableProperty] private string _theme = "Vaporwave";
    [ObservableProperty] private DateOnly _firstLaunchDate = DateOnly.FromDateTime(DateTime.Now);
    [ObservableProperty] private DateOnly _lastLaunchDate = DateOnly.FromDateTime(DateTime.Now);
    [ObservableProperty] private int _launchStreak = 1;
    [ObservableProperty] private int _bestLaunchStreak = 1;
    [ObservableProperty] private string _version = MainWindowViewModel.VersionText;
    [ObservableProperty] private bool _showTimer = true;
    [ObservableProperty] private bool _showProgress = true;
    [ObservableProperty] private bool _showSkipButton = true;
    [ObservableProperty] private bool _showRetryLaterButton = true;
    [ObservableProperty] private bool _showAnswerStreakInReview = true;
    [ObservableProperty] private bool _showAdditionalFieldLatexPreviews = true;
    [ObservableProperty] private bool _showBackgroundSwirl = true;
    [ObservableProperty] private bool _checkForUpdatesOnStartup = true;
    [ObservableProperty] private string _databasePath = TextUtility.DatabasePath;
    [ObservableProperty] private string? _supabaseAccessToken;
    [ObservableProperty] private string? _supabaseUserId;
    [ObservableProperty] private string? _supabaseUsername;
    [ObservableProperty] private DateTime _supabaseExpirationTime;

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
        ShowSkipButton = other.ShowSkipButton;
        ShowRetryLaterButton = other.ShowRetryLaterButton;
        ShowAnswerStreakInReview = other.ShowAnswerStreakInReview;
        ShowAdditionalFieldLatexPreviews = other.ShowAdditionalFieldLatexPreviews;
        ShowBackgroundSwirl = other.ShowBackgroundSwirl;
        DatabasePath = other.DatabasePath;
        SupabaseAccessToken = other.SupabaseAccessToken;
        SupabaseUserId = other.SupabaseUserId;
        SupabaseUsername = other.SupabaseUsername;
        SupabaseExpirationTime = other.SupabaseExpirationTime;
    }

    public void SetSupabase(string? accessToken, string? userID, string? userName, DateTime expiration)
    {
        SupabaseAccessToken = accessToken;
        SupabaseUserId = userID;
        SupabaseUsername = userName;
        SupabaseExpirationTime = expiration;
    }
}