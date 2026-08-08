using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReviFlash.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly Dictionary<string, ThemeVariant> ThemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pinks & Violets / Pinks (Synthwave, Sakura, Vaporwave, MidnightRose)
        { "Vaporwave", AppThemes.Vaporwave },
        { "Synthwave", AppThemes.Synthwave },
        { "Midnight Rose", AppThemes.MidnightRose },        
        { "Sakura", AppThemes.Sakura },

        // Neutrals & True Monochromes (Dark grays/blacks)
        { "Eclipse", AppThemes.Eclipse },
        { "Graphite", AppThemes.Graphite },
        { "Midnight Slate", AppThemes.MidnightSlate },
        { "Focus", AppThemes.Focus },
        { "Slate", AppThemes.Slate },

        // Reds & Crimson (Deep reds, wines, blood)
        { "Crimson", AppThemes.Crimson },
        { "Ember", AppThemes.Ember },
        { "Blood Moon", AppThemes.BloodMoon },


        // Purples (Nether, Amethyst, Void)
        { "Nether", AppThemes.Nether },
        { "Amethyst", AppThemes.Amethyst },
        { "Void", AppThemes.Void },

        // Blues & Teals (Cobalt, Midnight, Nordic, Ocean, Abyssal)
        { "Cobalt", AppThemes.Cobalt },
        { "Midnight", AppThemes.Midnight },
        { "Nordic", AppThemes.Nordic },
        { "Ocean", AppThemes.Ocean },
        { "Abyssal", AppThemes.Abyssal },

        // Greens (Matrix, Forest, Mint, DeepMoss, Toxic)
        { "Matrix", AppThemes.Matrix },
        { "Forest", AppThemes.Forest },
        { "Mint Choco", AppThemes.MintChoco },
        { "Toxic", AppThemes.Toxic },

        // Oranges & Warm Tones (Sunset, Coffee, Honeycomb, DarkAmber, SolarFlare, Bunker)
        { "Sunset", AppThemes.Sunset },
        { "Coffee", AppThemes.Coffee },
        { "Honeycomb", AppThemes.Honeycomb },
        { "Dark Amber", AppThemes.DarkAmber },
        { "Bunker", AppThemes.Bunker },

        // Cyberpunk (Multi-color neon)
        { "Cyberpunk", AppThemes.Cyberpunk },

        // Light Themes Section
        { "Sun", AppThemes.Sun },
        { "Desert", AppThemes.Desert },
        { "Sepia", AppThemes.Sepia },
        { "Rose", AppThemes.Rose },
        { "Plains", AppThemes.Plains },
    };

    public IEnumerable<string> AvailableThemes => ThemeMap.Keys;

    public ObservableCollection<FlashCardDeck> AvailableDecks { get; } = [];

    [ObservableProperty] private FlashCardDeck? _selectedDeckForStatDeletion;
    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private bool _showTimer;
    [ObservableProperty] private bool _showProgress;
    [ObservableProperty] private bool _showSkipButton;
    [ObservableProperty] private bool _showRetryLaterButton;
    [ObservableProperty] private bool _showAnswerStreakInReview;
    [ObservableProperty] private bool _showAdditionalFieldLatexPreviews;
    [ObservableProperty] private bool _showBackgroundSwirl;

    public SettingsViewModel()
    {
        _selectedTheme = MetaDataManager.Data.Theme;
        _showTimer = MetaDataManager.Data.ShowTimer;
        _showProgress = MetaDataManager.Data.ShowProgress;
        _showSkipButton = MetaDataManager.Data.ShowSkipButton;
        _showRetryLaterButton = MetaDataManager.Data.ShowRetryLaterButton;
        _showAnswerStreakInReview = MetaDataManager.Data.ShowAnswerStreakInReview;
        _showAdditionalFieldLatexPreviews = MetaDataManager.Data.ShowAdditionalFieldLatexPreviews;
        _showBackgroundSwirl = MetaDataManager.Data.ShowBackgroundSwirl;

        LoadDecks();
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

    public static void ApplyTheme(AppMetaData settings, string themeName)
    {
        settings.Theme = themeName;

        if (Application.Current != null)
        {
            if (ThemeMap.TryGetValue(themeName, out var variant)) Application.Current.RequestedThemeVariant = variant;
            else Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            App.ApplyAccessibilityPalette(false); // Default to dark theme for accessibility
        }
    }

    private void LoadDecks()
    {
        AvailableDecks.Clear();
        var decks = FlashCardRepository.GetAllDecks();
        foreach (var deck in decks)
        {
            AvailableDecks.Add(deck);
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

        LoadDecks();
    }

    public void DeleteStatsForSelectedDeck()
    {
        if (SelectedDeckForStatDeletion != null)
        {
            FlashCardRepository.DeleteStatsForDeck(SelectedDeckForStatDeletion.ID);
            SelectedDeckForStatDeletion = null;
        }
    }
}