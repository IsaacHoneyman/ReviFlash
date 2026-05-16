using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data;
using System.Linq;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public enum DeckSelectionMode
    {
        None,
        Review,
        Export,
    }

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
        set { _streakText = value; OnPropertyChanged(nameof(StreakText)); }
    }

    private string _bestEverStreakText = "0 Day Streak";
    public string BestEverStreakText
    {
        get => _bestEverStreakText;
        set { _bestEverStreakText = value; OnPropertyChanged(nameof(BestEverStreakText)); }
    }

    public static string VersionText => $"Version B-{GetAssemblyVersionText()}";

    private static string GetAssemblyVersionText()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "Unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
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

    private DeckSelectionMode _selectionMode = DeckSelectionMode.None;
    public DeckSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            OnPropertyChanged(nameof(SelectionMode));
            OnPropertyChanged(nameof(IsSelectionModeActive));
            OnPropertyChanged(nameof(IsReviewSelectionMode));
            OnPropertyChanged(nameof(IsExportSelectionMode));
            OnPropertyChanged(nameof(CanShowDeckManagementActions));
            OnPropertyChanged(nameof(ReviewSelectionButtonText));
            OnPropertyChanged(nameof(ExportSelectionButtonText));
        }
    }

    private readonly HashSet<ulong> _selectedDeckIds = [];
    public bool HasSelectedDecks => _selectedDeckIds.Count > 0;
    public int SelectedDeckCount => _selectedDeckIds.Count;
    public bool IsSelectionModeActive => SelectionMode != DeckSelectionMode.None;
    public bool IsReviewSelectionMode => SelectionMode == DeckSelectionMode.Review;
    public bool IsExportSelectionMode => SelectionMode == DeckSelectionMode.Export;
    public bool CanShowDeckManagementActions => !IsSelectionModeActive;
    public string ReviewSelectionButtonText => !IsReviewSelectionMode
        ? "Select Multiple"
        : HasSelectedDecks
            ? $"Play Selected ({SelectedDeckCount})"
            : "Cancel";
    public string ExportSelectionButtonText => !IsExportSelectionMode
        ? "Export"
        : HasSelectedDecks
            ? $"Export Selected ({SelectedDeckCount})"
            : "Cancel";

    private bool _showGroups = true;
    public bool ShowGroups
    {
        get => _showGroups;
        set
        {
            _showGroups = value;
            OnPropertyChanged(nameof(ShowGroups));
            RefreshDashboardItems();
        }
    }

    private bool _showSets = true;
    public bool ShowSets
    {
        get => _showSets;
        set
        {
            _showSets = value;
            OnPropertyChanged(nameof(ShowSets));
            RefreshDashboardItems();
        }
    }

    public bool ShowBackgroundSwirl => App.CurrentMetaData.ShowBackgroundSwirl;

    public static int CompareVersionNumber(string versionA, string versionB)
    {
        int[] aVersion = ExtractVersionNumber(versionA);
        int[] bVersion = ExtractVersionNumber(versionB);

        for (int i = 0; i < 3; i++)
        {
            if (aVersion[i] > bVersion[i]) return 1;
            if (aVersion[i] < bVersion[i]) return -1;
        }

        return 0; 
    }

    private static int[] ExtractVersionNumber(string version)
    {
        Match match = Regex.Match(version, @"(\d+)\.(\d+)\.(\d+)");

        if (!match.Success)
            return [0, 0, 0];

        return
        [   int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value) ];
    }

    private TimePeriodOption _selectedTimePeriod = null!;
    public TimePeriodOption SelectedTimePeriod
    {
        get => _selectedTimePeriod;
        set
        {
            _selectedTimePeriod = value;
            OnPropertyChanged(nameof(SelectedTimePeriod));
            if (IsViewingDeckStats && SelectedDeckForStats != null)
            {
                ShowDeckStats(SelectedDeckForStats);
            }
            else
            {
                LoadStats();
            }
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

    private int _totalTimeSeconds = 0;
    public int TotalTimeSeconds
    {
        get => _totalTimeSeconds;
        set
        {
            _totalTimeSeconds = value;
            OnPropertyChanged(nameof(TotalTimeSeconds));
            OnPropertyChanged(nameof(TotalTimeFormatted));
        }
    }

    public string TotalTimeFormatted
    {
        get
        {
            var time = TimeSpan.FromSeconds(TotalTimeSeconds);
            return time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");
        }
    }

    private bool _isViewingDeckStats = false;
    public bool IsViewingDeckStats
    {
        get => _isViewingDeckStats;
        set 
        { 
            _isViewingDeckStats = value; 
            OnPropertyChanged(nameof(IsViewingDeckStats));
            OnPropertyChanged(nameof(IsViewingAnyStats));
        }
    }

    private FlashCardDeck? _selectedDeckForStats = null;
    public FlashCardDeck? SelectedDeckForStats
    {
        get => _selectedDeckForStats;
        set { _selectedDeckForStats = value; OnPropertyChanged(nameof(SelectedDeckForStats)); }
    }

    private bool _isViewingGroupStats = false;
    public bool IsViewingGroupStats
    {
        get => _isViewingGroupStats;
        set 
        { 
            _isViewingGroupStats = value; 
            OnPropertyChanged(nameof(IsViewingGroupStats));
            OnPropertyChanged(nameof(IsViewingAnyStats));
        }
    }

    private StudyGroup? _selectedGroupForStats = null;
    public StudyGroup? SelectedGroupForStats
    {
        get => _selectedGroupForStats;
        set { _selectedGroupForStats = value; OnPropertyChanged(nameof(SelectedGroupForStats)); }
    }

    public bool IsViewingAnyStats => IsViewingDeckStats || IsViewingGroupStats;

    public ObservableCollection<TimePeriodOption> TimePeriods { get; set; } = [];
    public ObservableCollection<FlashCardDeck> Decks { get; set; } = [];
    public ObservableCollection<StudyGroup> StudyGroups { get; set; } = [];
    public ObservableCollection<object> DashboardItems { get; set; } = [];
    public ObservableCollection<FlashCardDeck> FilteredDecks { get; set; } = [];

    public class SortOption
    {
        public string Label { get; set; } = "";
        public string Key { get; set; } = ""; // internal key used for switching

        public SortOption(string label, string key)
        {
            Label = label;
            Key = key;
        }

        public override string ToString() => Label;
    }

    public ObservableCollection<SortOption> SortOptions { get; } = new();

    private SortOption? _selectedSortOption = null;
    public SortOption? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            _selectedSortOption = value;
            OnPropertyChanged(nameof(SelectedSortOption));
            FilterDecks();
        }
    }

    public MainWindowViewModel()
    {
        App.CurrentMetaDataChanged += _ => OnPropertyChanged(nameof(ShowBackgroundSwirl));

        var meta = App.CurrentMetaData;
        StreakText = $"{meta.LaunchStreak} Day Streak";
        BestEverStreakText = $"{meta.BestLaunchStreak} Days";
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

        // Sorting options (default alphabetical A-Z)
        SortOptions.Add(new SortOption("Name A-Z", "name_asc"));
        SortOptions.Add(new SortOption("Name Z-A", "name_desc"));
        SortOptions.Add(new SortOption("Cards (high → low)", "cards_desc"));
        SortOptions.Add(new SortOption("Cards (low → high)", "cards_asc"));
        SortOptions.Add(new SortOption("Study time (high → low)", "studytime_desc"));
        SortOptions.Add(new SortOption("Study time (low → high)", "studytime_asc"));

        SelectedSortOption = SortOptions[0];

        LoadDecksFromDatabase();
        FilterDecks();
        RefreshDashboardItems();
    }

    private void LoadStats()
    {
        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(null, timeModifier);

        TotalQuestions = total;
        TotalCorrect = correct;
        TotalTimeSeconds = timeTakenSeconds;
        Percentage = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;
        Grade = GradeCalculator.CalculateGradeWithDefault(correct, total);
    }

    public void RefreshStats()
    {
        if (IsViewingDeckStats && SelectedDeckForStats != null)
        {
            ShowDeckStats(SelectedDeckForStats);
        }
        else if (IsViewingGroupStats && SelectedGroupForStats != null)
        {
            ShowGroupStats(SelectedGroupForStats);
        }
        else
        {
            LoadStats();
        }
    }

    public void ShowDeckStats(FlashCardDeck deck)
    {
        SelectedDeckForStats = deck;
        IsViewingDeckStats = true;
        IsViewingGroupStats = false;

        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total, timeTakenSeconds, percentage, grade) = GetDeckStats(deck.ID, timeModifier);
        TotalQuestions = total;
        TotalCorrect = correct;
        TotalTimeSeconds = timeTakenSeconds;
        Percentage = percentage;
        Grade = grade;
    }

    public void ShowGroupStats(StudyGroup group)
    {
        SelectedGroupForStats = group;
        IsViewingGroupStats = true;
        IsViewingDeckStats = false;

        var timeModifier = SelectedTimePeriod?.TimeModifier;
        
        // Get all decks in the group and sum their stats
        var decksInGroup = FlashCardRepository.GetDecksForStudyGroup(group.ID);
        
        int totalCorrect = 0;
        int totalQuestions = 0;
        int totalSeconds = 0;

        foreach (var deck in decksInGroup)
        {
            var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(deck.ID, timeModifier);
            totalCorrect += correct;
            totalQuestions += total;
            totalSeconds += timeTakenSeconds;
        }

        double percentage = totalQuestions > 0 ? Math.Round((double)totalCorrect / totalQuestions * 100, 1) : 0;
        string grade = GradeCalculator.CalculateGradeWithDefault(totalCorrect, totalQuestions);

        TotalQuestions = totalQuestions;
        TotalCorrect = totalCorrect;
        TotalTimeSeconds = totalSeconds;
        Percentage = percentage;
        Grade = grade;
    }

    public void ShowOverallStats()
    {
        IsViewingDeckStats = false;
        IsViewingGroupStats = false;
        SelectedDeckForStats = null;
        SelectedGroupForStats = null;
        LoadStats();
    }

    public (int correct, int total, int timeTakenSeconds, double percentage, string grade) GetDeckStats(ulong deckID, string? timeModifier = null)
    {
        var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(deckID, timeModifier);
        double percentage = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;

        string grade = GradeCalculator.CalculateGradeWithDefault(correct, total);

        return (correct, total, timeTakenSeconds, percentage, grade);
    }

    public void FilterDecks()
    {
        FilteredDecks.Clear();

        List<FlashCardDeck> resultsList;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            resultsList = Decks.ToList();
        }
        else
        {
            var lowerSearch = SearchText.ToLower();
            resultsList = Decks.Where(d => d.Name.ToLower().Contains(lowerSearch)).ToList();
        }

        // Apply selected sort
        if (SelectedSortOption != null)
        {
            switch (SelectedSortOption.Key)
            {
                case "name_asc":
                    resultsList = resultsList.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case "name_desc":
                    resultsList = resultsList.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case "cards_asc":
                    resultsList = resultsList.OrderBy(d => d.CardCount).ToList();
                    break;
                case "cards_desc":
                    resultsList = resultsList.OrderByDescending(d => d.CardCount).ToList();
                    break;
                case "studytime_asc":
                    resultsList = resultsList.OrderBy(d => FlashCardRepository.GetStats(d.ID).timeTakenSeconds).ToList();
                    break;
                case "studytime_desc":
                    resultsList = resultsList.OrderByDescending(d => FlashCardRepository.GetStats(d.ID).timeTakenSeconds).ToList();
                    break;
                default:
                    break;
            }
        }

        foreach (var deck in resultsList)
        {
            deck.IsSelectedForMultiReview = _selectedDeckIds.Contains(deck.ID);
            FilteredDecks.Add(deck);
        }

        RefreshDashboardItems();
    }

    public void BeginReviewSelection()
    {
        BeginSelectionMode(DeckSelectionMode.Review);
    }

    public void BeginExportSelection()
    {
        BeginSelectionMode(DeckSelectionMode.Export);
    }

    public void CancelSelectionMode()
    {
        SelectionMode = DeckSelectionMode.None;
        _selectedDeckIds.Clear();
        foreach (var deck in Decks)
        {
            deck.IsSelectedForMultiReview = false;
        }

        NotifySelectionChanged();
        FilterDecks();
    }

    public void ToggleDeckSelection(FlashCardDeck deck)
    {
        if (_selectedDeckIds.Contains(deck.ID))
        {
            _selectedDeckIds.Remove(deck.ID);
            deck.IsSelectedForMultiReview = false;
        }
        else
        {
            _selectedDeckIds.Add(deck.ID);
            deck.IsSelectedForMultiReview = true;
        }

        NotifySelectionChanged();
        FilterDecks();
    }

    public List<FlashCardDeck> GetSelectedDecks() =>
        Decks.Where(d => _selectedDeckIds.Contains(d.ID)).ToList();

    private void BeginSelectionMode(DeckSelectionMode mode)
    {
        SelectionMode = mode;
        _selectedDeckIds.Clear();

        foreach (var deck in Decks)
        {
            deck.IsSelectedForMultiReview = false;
        }

        NotifySelectionChanged();
        FilterDecks();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedDecks));
        OnPropertyChanged(nameof(SelectedDeckCount));
        OnPropertyChanged(nameof(ReviewSelectionButtonText));
        OnPropertyChanged(nameof(ExportSelectionButtonText));
    }

    public void DeleteDeck(FlashCardDeck deckToDelete)
    {
        FlashCardRepository.DeleteDeck(deckToDelete.ID);
        Decks.Remove(deckToDelete);
        _selectedDeckIds.Remove(deckToDelete.ID);
        NotifySelectionChanged();
        LoadStudyGroupsFromDatabase();
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

        LoadStudyGroupsFromDatabase();
    }

    public void LoadStudyGroupsFromDatabase()
    {
        var savedGroups = FlashCardRepository.GetAllStudyGroups();
        StudyGroups.Clear();
        foreach (var group in savedGroups)
        {
            StudyGroups.Add(group);
        }

        RefreshDashboardItems();
    }

    public void DeleteStudyGroup(StudyGroup groupToDelete)
    {
        FlashCardRepository.DeleteStudyGroup(groupToDelete.ID);
        StudyGroups.Remove(groupToDelete);
        RefreshDashboardItems();
    }

    public void RefreshAfterBackupRestore()
    {
        StreakText = $"{App.CurrentMetaData.LaunchStreak} Day Streak";
        BestEverStreakText = $"{App.CurrentMetaData.BestLaunchStreak} Days";
        CancelSelectionMode();
        LoadDecksFromDatabase();
        FilterDecks();
        RefreshStats();
    }

    public void CreateNewDeck()
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        FlashCardRepository.SaveNewDeck(newDeck);

        LoadDecksFromDatabase();
        FilterDecks();
    }

    private void RefreshDashboardItems()
    {
        DashboardItems.Clear();

        if (ShowGroups)
        {
            foreach (var group in StudyGroups)
            {
                DashboardItems.Add(group);
            }
        }

        if (ShowSets)
        {
            foreach (var deck in FilteredDecks)
            {
                DashboardItems.Add(deck);
            }
        }
    }

    public void EditDeck(FlashCardDeck deckToEdit)
    {
        System.Console.WriteLine($"Opening editor for: {deckToEdit.Name}");
    }
}