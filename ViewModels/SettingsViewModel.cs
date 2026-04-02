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
        "Dark",
        "Light",
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
        get => App.CurrentMetaData.Theme;
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

    public static void ApplyTheme(string themeName)
    {
        App.CurrentMetaData.Theme = themeName;

        if (Application.Current != null)
        {
            if (themeName == "Dark")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            else if (themeName == "Light")
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
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