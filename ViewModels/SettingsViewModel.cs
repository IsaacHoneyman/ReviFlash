using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Data;
using ReviFlash.Models;
using System.IO;

namespace ReviFlash.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly AppMetaData _settings;

    // 1. The list of themes that the ComboBox will display
    public List<string> AvailableThemes { get; } = new()
    {
        "Default",
        "Midnight",
        "Forest",
        "Focus",
        "Amethyst",
        "Slate",
        "Ember",
        "Crimson",
        "Light",
        "Desert",
        "Sepia",
        "Sun",
        "Rose",
        "Plains",
        "Water",
        "Pride",
    };

    private ObservableCollection<FlashCardDeck> _availableDecks = new();
    public ObservableCollection<FlashCardDeck> AvailableDecks
    {
        get => _availableDecks;
        set { _availableDecks = value; OnPropertyChanged(nameof(AvailableDecks)); }
    }

    private FlashCardDeck? _selectedDeckForStatDeletion = null;
    public FlashCardDeck? SelectedDeckForStatDeletion
    {
        get => _selectedDeckForStatDeletion;
        set { _selectedDeckForStatDeletion = value; OnPropertyChanged(nameof(SelectedDeckForStatDeletion)); }
    }

    public string DatabasePath
    {
        get => _settings.DatabasePath;
        set
        {
            _settings.DatabasePath = string.IsNullOrWhiteSpace(value)
                ? AppStoragePaths.DatabasePath
                : Path.GetFullPath(value);
            DatabaseManager.ConfigureDatabasePath(_settings.DatabasePath);
            OnPropertyChanged(nameof(DatabasePath));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public string SelectedTheme
    {
        get => _settings.Theme == "Dark" ? "Default" : _settings.Theme;
        set
        {
            ApplyTheme(_settings, value);
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowTimer
    {
        get => _settings.ShowTimer;
        set
        {
            _settings.ShowTimer = value;
            OnPropertyChanged(nameof(ShowTimer));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowProgress
    {
        get => _settings.ShowProgress;
        set
        {
            _settings.ShowProgress = value;
            OnPropertyChanged(nameof(ShowProgress));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowSkipButton
    {
        get => _settings.ShowSkipButton;
        set
        {
            _settings.ShowSkipButton = value;
            OnPropertyChanged(nameof(ShowSkipButton));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowRetryLaterButton
    {
        get => _settings.ShowRetryLaterButton;
        set
        {
            _settings.ShowRetryLaterButton = value;
            OnPropertyChanged(nameof(ShowRetryLaterButton));
            MetaDataManager.SaveMetaData(_settings);
        }
    }
    
    public bool ShowAnswerStreakInReview
    {
        get => _settings.ShowAnswerStreakInReview;
        set
        {
            _settings.ShowAnswerStreakInReview = value;
            OnPropertyChanged(nameof(ShowAnswerStreakInReview));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowAdditionalFieldLatexPreviews
    {
        get => _settings.ShowAdditionalFieldLatexPreviews;
        set
        {
            _settings.ShowAdditionalFieldLatexPreviews = value;
            OnPropertyChanged(nameof(ShowAdditionalFieldLatexPreviews));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public bool ShowBackgroundSwirl
    {
        get => _settings.ShowBackgroundSwirl;
        set
        {
            _settings.ShowBackgroundSwirl = value;
            OnPropertyChanged(nameof(ShowBackgroundSwirl));
            MetaDataManager.SaveMetaData(_settings);
        }
    }

    public SettingsViewModel(AppMetaData settings)
    {
        _settings = settings;
        _settings.PropertyChanged += Settings_PropertyChanged;
        DatabaseManager.ConfigureDatabasePath(_settings.DatabasePath);
        LoadDecks();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppMetaData.Theme):
                OnPropertyChanged(nameof(SelectedTheme));
                break;
            case nameof(AppMetaData.ShowTimer):
                OnPropertyChanged(nameof(ShowTimer));
                break;
            case nameof(AppMetaData.ShowProgress):
                OnPropertyChanged(nameof(ShowProgress));
                break;
            case nameof(AppMetaData.ShowSkipButton):
                OnPropertyChanged(nameof(ShowSkipButton));
                break;
            case nameof(AppMetaData.ShowRetryLaterButton):
                OnPropertyChanged(nameof(ShowRetryLaterButton));
                break;
            case nameof(AppMetaData.ShowAnswerStreakInReview):
                OnPropertyChanged(nameof(ShowAnswerStreakInReview));
                break;
            case nameof(AppMetaData.ShowAdditionalFieldLatexPreviews):
                OnPropertyChanged(nameof(ShowAdditionalFieldLatexPreviews));
                break;
            case nameof(AppMetaData.ShowBackgroundSwirl):
                OnPropertyChanged(nameof(ShowBackgroundSwirl));
                break;
            case nameof(AppMetaData.DatabasePath):
                OnPropertyChanged(nameof(DatabasePath));
                break;
        }
    }

    public static void ApplyTheme(AppMetaData settings, string themeName)
    {
        if (themeName == "Pastel")
        {
            themeName = "Plains";
        }

        settings.Theme = themeName == "Default" ? "Default" : themeName;

        if (Application.Current != null)
        {
            if (themeName == "Default" || themeName == "Dark")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            else if (themeName == "Light")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else if (themeName == "Midnight")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Midnight;
            }
            else if (themeName == "Forest")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Forest;
            }
            else if (themeName == "Desert")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Desert;
            }
            else if (themeName == "Sepia")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Sepia;
            }
            else if (themeName == "Sun")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Sun;
            }
            else if (themeName == "Slate")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Slate;
            }
            else if (themeName == "Ember")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Ember;
            }
            else if (themeName == "Crimson")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Crimson;
            }
            else if (themeName == "Focus")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Focus;
            }
            else if (themeName == "Amethyst")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Amethyst;
            }
            else if (themeName == "Rose")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Rose;
            }
            else if (themeName == "Plains")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Plains;
            }
            else if (themeName == "Water")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Water;
            }
            else if (themeName == "Pride")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Pride;
            }
            else
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }

            App.ApplyAccessibilityPalette(App.IsLightThemeName(themeName));
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
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(ShowTimer));
        OnPropertyChanged(nameof(ShowProgress));
        OnPropertyChanged(nameof(ShowSkipButton));
        OnPropertyChanged(nameof(ShowRetryLaterButton));
        OnPropertyChanged(nameof(ShowAnswerStreakInReview));
        OnPropertyChanged(nameof(ShowBackgroundSwirl));
        OnPropertyChanged(nameof(DatabasePath));
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