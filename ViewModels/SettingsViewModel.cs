using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;
using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReviFlash.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    // Round the colour wheel from the pinks (Vaporwave, the default, first) through reds, oranges, greens and blues to purples, then the greys.
    private static readonly Dictionary<string, ThemeVariant> ThemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Vaporwave", AppThemes.Vaporwave },
        { "Synthwave", AppThemes.Synthwave },
        { "Sakura", AppThemes.Sakura },
        { "Crimson", AppThemes.Crimson },
        { "Ember", AppThemes.Ember },
        { "Sunset", AppThemes.Sunset },
        { "Coffee", AppThemes.Coffee },
        { "Dark Amber", AppThemes.DarkAmber },
        { "Honeycomb", AppThemes.Honeycomb },
        { "Forest", AppThemes.Forest },
        { "Abyssal", AppThemes.Abyssal },
        { "Nordic", AppThemes.Nordic },
        { "Slate", AppThemes.Slate },
        { "Amethyst", AppThemes.Amethyst },
        { "Cyberpunk", AppThemes.Cyberpunk },
        { "Nether", AppThemes.Nether },
        { "Focus", AppThemes.Focus },
        { "Graphite", AppThemes.Graphite },
    };

    public const string DefaultTheme = "Vaporwave";

    public static IReadOnlyDictionary<string, ThemeVariant> Themes => ThemeMap;

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
    [ObservableProperty] private bool _showChangelogAfterUpdate;

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
        _showChangelogAfterUpdate = MetaDataManager.Data.ShowChangelogAfterUpdate;
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


    partial void OnShowTimerChanged(bool value) => Save(data => data.ShowTimer = value);
    partial void OnShowProgressChanged(bool value) => Save(data => data.ShowProgress = value);
    partial void OnShowSkipButtonChanged(bool value) => Save(data => data.ShowSkipButton = value);
    partial void OnShowRetryLaterButtonChanged(bool value) => Save(data => data.ShowRetryLaterButton = value);
    partial void OnShowAnswerStreakInReviewChanged(bool value) => Save(data => data.ShowAnswerStreakInReview = value);
    partial void OnShowAdditionalFieldLatexPreviewsChanged(bool value) => Save(data => data.ShowAdditionalFieldLatexPreviews = value);
    partial void OnShowBackgroundSwirlChanged(bool value) => Save(data => data.ShowBackgroundSwirl = value);
    partial void OnUseLatexFontForCardsChanged(bool value) => Save(data => data.UseLatexFontForCards = value);
    partial void OnCheckForUpdatesOnStartupChanged(bool value) => Save(data => data.CheckForUpdatesOnStartup = value);
    partial void OnShowChangelogAfterUpdateChanged(bool value) => Save(data => data.ShowChangelogAfterUpdate = value);

    private static void Save(Action<AppMetaData> apply)
    {
        apply(MetaDataManager.Data);
        MetaDataManager.SaveMetaData();
    }

    public static void ApplyTheme(AppMetaData settings, string themeName)
    {
        var saved = ThemeMap.ContainsKey(themeName) ? null
            : settings.SavedThemes.FirstOrDefault(theme => string.Equals(theme.Name, themeName, StringComparison.OrdinalIgnoreCase));
        // A theme that's since been removed or deleted falls back to the default.
        if (saved is not null) themeName = saved.Name;
        else if (!ThemeMap.ContainsKey(themeName)) themeName = DefaultTheme;
        settings.Theme = themeName;

        if (Application.Current != null)
        {
            var variant = saved is not null ? CustomThemes.Install(saved) : ThemeMap[themeName];
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
        ShowChangelogAfterUpdate = MetaDataManager.Data.ShowChangelogAfterUpdate;
    }
}