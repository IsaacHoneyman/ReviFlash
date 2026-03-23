using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Linq;
using System;

namespace ReviFlash.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public class TimePeriodOption
    {
        public string Label { get; set; }
        public string TimeModifier { get; set; }

        public TimePeriodOption(string label, string timeModifier)
        {
            Label = label;
            TimeModifier = timeModifier;
        }
    }

    private object _currentPage = new();
    public object CurrentPage
    {
        get => _currentPage;
        set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); }
    }

    private string _streakText = "0 Day Streak";
    public string StreakText
    {
        get => _streakText;
        set => _streakText = value;
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            FilterDecks();
        }
    }

    private TimePeriodOption _selectedTimePeriod = null!;
    public TimePeriodOption SelectedTimePeriod
    {
        get => _selectedTimePeriod;
        set
        {
            _selectedTimePeriod = value;
            OnPropertyChanged(nameof(SelectedTimePeriod));
            LoadStats();
        }
    } 

    private int _totalQuestions = 0;
    public int TotalQuestions
    {
        get => _totalQuestions;
        set { _totalQuestions = value; OnPropertyChanged(nameof(TotalQuestions)); }
    }

    private int _totalCorrect = 0;
    public int TotalCorrect
    {
        get => _totalCorrect;
        set { _totalCorrect = value; OnPropertyChanged(nameof(TotalCorrect)); }
    }

    private double _percentage = 0;
    public double Percentage
    {
        get => _percentage;
        set { _percentage = value; OnPropertyChanged(nameof(Percentage)); }
    }

    private string _grade = "U";
    public string Grade
    {
        get => _grade;
        set { _grade = value; OnPropertyChanged(nameof(Grade)); }
    }

    public ObservableCollection<TimePeriodOption> TimePeriods { get; set; } = [];
    public ObservableCollection<FlashCardDeck> Decks { get; set; } = [];
    public ObservableCollection<FlashCardDeck> FilteredDecks { get; set; } = [];

    public MainWindowViewModel()
    {
        var meta = App.CurrentMetaData;
        StreakText = $"{meta.LaunchStreak} Day Streak";
        CurrentPage = this; 

        // Initialize time period options
        TimePeriods.Add(new TimePeriodOption("All Time", null!));
        TimePeriods.Add(new TimePeriodOption("Last 6 Months", "-6 months"));
        TimePeriods.Add(new TimePeriodOption("Last 3 Months", "-3 months"));
        TimePeriods.Add(new TimePeriodOption("Last Month", "-1 months"));
        TimePeriods.Add(new TimePeriodOption("Last 2 Weeks", "-14 days"));
        TimePeriods.Add(new TimePeriodOption("Last Week", "-7 days"));
        TimePeriods.Add(new TimePeriodOption("Last 3 Days", "-3 days"));
        TimePeriods.Add(new TimePeriodOption("Last Day", "-1 days"));

        // Set "All Time" as default
        SelectedTimePeriod = TimePeriods[0];

        LoadDecksFromDatabase();
        FilterDecks();
    }

    private void LoadStats()
    {
        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total) = FlashCardRepository.GetStats(null, timeModifier);

        TotalQuestions = total;
        TotalCorrect = correct;
        Percentage = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;
        
        // Calculate grade using same logic as SummaryViewModel
        Grade = Percentage switch
        {
            >= 90 => "A*",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            >= 50 => "D",
            _ => "U"
        };
    }

    public void FilterDecks()
    {
        FilteredDecks.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var deck in Decks) FilteredDecks.Add(deck);
        }
        else
        {
            var lowerSearch = SearchText.ToLower();
            var results = Decks.Where(d => d.Name.ToLower().Contains(lowerSearch));

            foreach (var deck in results) FilteredDecks.Add(deck);
        }
    }

    public void DeleteDeck(FlashCardDeck deckToDelete)
    {
        FlashCardRepository.DeleteDeck(deckToDelete.ID);
        Decks.Remove(deckToDelete);
        FilterDecks();
    }

    public void LoadDecksFromDatabase()
    {
        var savedDecks = FlashCardRepository.GetAllDecks();
        Decks.Clear();
        foreach (var deck in savedDecks)
        {
            Decks.Add(deck);
        }
    }

    public void CreateNewDeck()
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        FlashCardRepository.SaveNewDeck(newDeck);

        LoadDecksFromDatabase();
        FilterDecks();
    }

    public void EditDeck(FlashCardDeck deckToEdit)
    {
        System.Console.WriteLine($"Opening editor for: {deckToEdit.Name}");
    }
}