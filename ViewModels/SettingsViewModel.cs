using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using ReviFlash.Data;
using ReviFlash.Models;

namespace ReviFlash.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    // 1. The list of themes that the ComboBox will display
    public List<string> AvailableThemes { get; } = new()
    {
        "Default",
        "Light",
        "Midnight",
        "Focus",
        "Forest",
        "Desert",
        "Sepia",
        "Contrast",
        "Sun",
        "Water",
        "Amethyst",
        "Rose",
        "Plains",
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

    public string SelectedTheme
    {
        get => App.CurrentMetaData.Theme == "Dark" ? "Default" : App.CurrentMetaData.Theme;
        set
        {
            ApplyTheme(value);
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public bool ShowTimer
    {
        get => App.CurrentMetaData.ShowTimer;
        set
        {
            App.CurrentMetaData.ShowTimer = value;
            OnPropertyChanged(nameof(ShowTimer));
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public bool ShowProgress
    {
        get => App.CurrentMetaData.ShowProgress;
        set
        {
            App.CurrentMetaData.ShowProgress = value;
            OnPropertyChanged(nameof(ShowProgress));
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public bool ShowAdditionalFieldLatexPreviews
    {
        get => App.CurrentMetaData.ShowAdditionalFieldLatexPreviews;
        set
        {
            App.CurrentMetaData.ShowAdditionalFieldLatexPreviews = value;
            OnPropertyChanged(nameof(ShowAdditionalFieldLatexPreviews));
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public bool ShowBackgroundSwirl
    {
        get => App.CurrentMetaData.ShowBackgroundSwirl;
        set
        {
            App.CurrentMetaData.ShowBackgroundSwirl = value;
            OnPropertyChanged(nameof(ShowBackgroundSwirl));
            App.SetCurrentMetaData(App.CurrentMetaData);
            MetaDataManager.SaveMetaData(App.CurrentMetaData);
        }
    }

    public static void ApplyTheme(string themeName)
    {
        if (themeName == "Pastel")
        {
            themeName = "Plains";
        }

        App.CurrentMetaData.Theme = themeName == "Default" ? "Default" : themeName;

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
            else if (themeName == "Contrast")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Contrast;
            }
            else if (themeName == "Sun")
            {
                Application.Current.RequestedThemeVariant = AppThemes.Sun;
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
        }
    }

    public SettingsViewModel()
    {
        LoadDecks();
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
        OnPropertyChanged(nameof(ShowBackgroundSwirl));
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