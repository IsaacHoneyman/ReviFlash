using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReviFlash.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Dictionary<string, ThemeVariant> ThemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pinks & Violets
        { "Vaporwave", AppThemes.Vaporwave },
        { "Synthwave", AppThemes.Synthwave },
        { "Midnight Rose", AppThemes.MidnightRose },        
        { "Sakura", AppThemes.Sakura },

        // Neutrals & Monochromes
        { "Eclipse", AppThemes.Eclipse },
        { "Graphite", AppThemes.Graphite },
        { "Midnight Slate", AppThemes.MidnightSlate },
        { "Focus", AppThemes.Focus },
        { "Slate", AppThemes.Slate },

        // Reds
        { "Crimson", AppThemes.Crimson },
        { "Ember", AppThemes.Ember },
        { "Blood Moon", AppThemes.BloodMoon },


        // Purples
        { "Nether", AppThemes.Nether },
        { "Amethyst", AppThemes.Amethyst },
        { "Void", AppThemes.Void },

        // Blues & Teals
        { "Cobalt", AppThemes.Cobalt },
        { "Midnight", AppThemes.Midnight },
        { "Nordic", AppThemes.Nordic },
        { "Ocean", AppThemes.Ocean },
        { "Abyssal", AppThemes.Abyssal },

        // Greens
        { "Matrix", AppThemes.Matrix },
        { "Forest", AppThemes.Forest },
        { "Mint Choco", AppThemes.MintChoco },
        { "Toxic", AppThemes.Toxic },

        // Oranges & Warm Tones
        { "Sunset", AppThemes.Sunset },
        { "Coffee", AppThemes.Coffee },
        { "Honeycomb", AppThemes.Honeycomb },
        { "Dark Amber", AppThemes.DarkAmber },
        { "Bunker", AppThemes.Bunker },

        // Multi-colour neon
        { "Cyberpunk", AppThemes.Cyberpunk },

        // Light themes
        { "Sun", AppThemes.Sun },
        { "Desert", AppThemes.Desert },
        { "Sepia", AppThemes.Sepia },
        { "Rose", AppThemes.Rose },
        { "Plains", AppThemes.Plains },
    };

    public IEnumerable<string> AvailableThemes => ThemeMap.Keys;

    // --- Account ---

    public bool IsSignedIn => AuthSession.IsSignedIn;
    public string AccountText => IsSignedIn ? $"Signed in as {AuthSession.Username}" : "Not signed in";

    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private bool _showTimer;
    [ObservableProperty] private bool _showProgress;
    [ObservableProperty] private bool _showSkipButton;
    [ObservableProperty] private bool _showRetryLaterButton;
    [ObservableProperty] private bool _showAnswerStreakInReview;
    [ObservableProperty] private bool _showAdditionalFieldLatexPreviews;
    [ObservableProperty] private bool _showBackgroundSwirl;
    [ObservableProperty] private bool _useLatexFontForCards;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;

    public SettingsViewModel()
    {
        // Sign-ins and sign-outs from anywhere (including this window) show up live.
        MetaDataManager.Data.PropertyChanged += Metadata_PropertyChanged;

        _selectedTheme = MetaDataManager.Data.Theme;
        _showTimer = MetaDataManager.Data.ShowTimer;
        _showProgress = MetaDataManager.Data.ShowProgress;
        _showSkipButton = MetaDataManager.Data.ShowSkipButton;
        _showRetryLaterButton = MetaDataManager.Data.ShowRetryLaterButton;
        _showAnswerStreakInReview = MetaDataManager.Data.ShowAnswerStreakInReview;
        _showAdditionalFieldLatexPreviews = MetaDataManager.Data.ShowAdditionalFieldLatexPreviews;
        _showBackgroundSwirl = MetaDataManager.Data.ShowBackgroundSwirl;
        _useLatexFontForCards = MetaDataManager.Data.UseLatexFontForCards;
        _checkForUpdatesOnStartup = MetaDataManager.Data.CheckForUpdatesOnStartup;
    }

    /// <summary> Stops listening to the app-wide metadata; call when the window closes. </summary>
    public void Detach() => MetaDataManager.Data.PropertyChanged -= Metadata_PropertyChanged;

    private void Metadata_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppMetaData.SupabaseRefreshToken):
            case nameof(AppMetaData.SupabaseAccessToken):
            case nameof(AppMetaData.SupabaseUsername):
            case nameof(AppMetaData.SupabaseExpirationTime):
                OnPropertyChanged(nameof(IsSignedIn));
                OnPropertyChanged(nameof(AccountText));
                break;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyTheme(MetaDataManager.Data, value);
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowTimerChanged(bool value)
    {
        MetaDataManager.Data.ShowTimer = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowProgressChanged(bool value)
    {
        MetaDataManager.Data.ShowProgress = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowSkipButtonChanged(bool value)
    {
        MetaDataManager.Data.ShowSkipButton = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowRetryLaterButtonChanged(bool value)
    {
        MetaDataManager.Data.ShowRetryLaterButton = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowAnswerStreakInReviewChanged(bool value)
    {
        MetaDataManager.Data.ShowAnswerStreakInReview = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowAdditionalFieldLatexPreviewsChanged(bool value)
    {
        MetaDataManager.Data.ShowAdditionalFieldLatexPreviews = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnShowBackgroundSwirlChanged(bool value)
    {
        MetaDataManager.Data.ShowBackgroundSwirl = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnUseLatexFontForCardsChanged(bool value)
    {
        MetaDataManager.Data.UseLatexFontForCards = value;
        MetaDataManager.SaveMetaData();
    }

    partial void OnCheckForUpdatesOnStartupChanged(bool value)
    {
        MetaDataManager.Data.CheckForUpdatesOnStartup = value;
        MetaDataManager.SaveMetaData();
    }

    public static void ApplyTheme(AppMetaData settings, string themeName)
    {
        settings.Theme = themeName;

        if (Application.Current != null)
        {
            var variant = ThemeMap.TryGetValue(themeName, out var mapped) ? mapped : ThemeVariant.Dark;
            Application.Current.RequestedThemeVariant = variant;

            // Light palettes need the darker accent set, or reds and greens wash out.
            App.ApplyAccessibilityPalette(App.IsLightTheme(variant));
        }
    }

    public void RefreshFromMetadata()
    {
        SelectedTheme = MetaDataManager.Data.Theme;
        ShowTimer = MetaDataManager.Data.ShowTimer;
        ShowProgress = MetaDataManager.Data.ShowProgress;
        ShowSkipButton = MetaDataManager.Data.ShowSkipButton;
        ShowRetryLaterButton = MetaDataManager.Data.ShowRetryLaterButton;
        ShowAnswerStreakInReview = MetaDataManager.Data.ShowAnswerStreakInReview;
        ShowAdditionalFieldLatexPreviews = MetaDataManager.Data.ShowAdditionalFieldLatexPreviews;
        ShowBackgroundSwirl = MetaDataManager.Data.ShowBackgroundSwirl;
        UseLatexFontForCards = MetaDataManager.Data.UseLatexFontForCards;
        CheckForUpdatesOnStartup = MetaDataManager.Data.CheckForUpdatesOnStartup;
    }
}