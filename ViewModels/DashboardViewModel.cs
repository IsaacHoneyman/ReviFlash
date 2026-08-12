using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using System.Linq;
using System;
using System.Collections.Generic;

using static ReviFlash.Utilities.CardUtility;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private enum StatsScope { Overall, Deck, Group, }
    public enum DeckSelectionMode { None, Review, Export, }

    private const string SORT_NAME_ASC = "name_asc";
    private const string SORT_NAME_DESC = "name_desc";
    private const string SORT_CARDS_ASC = "cards_asc";
    private const string SORT_CARDS_DESC = "cards_desc";
    private const string SORT_STUDYTIME_ASC = "studytime_asc";
    private const string SORT_STUDYTIME_DESC = "studytime_desc";

    public class TimePeriodOption(string label, string timeModifier)
    {
        public string Label { get; set; } = label;
        public string TimeModifier { get; set; } = timeModifier;
    }

    public class SortOption(string label, string key)
    {
        public string Label { get; set; } = label;
        public string Key { get; set; } = key;

        public override string ToString() => Label;
    }

    [ObservableProperty] private object _currentPage = new();
    [ObservableProperty] private string _bestAnswerStreakText = "0";
    [ObservableProperty] private bool _isGraphView;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _streakText = "0 Day Streak";
    [ObservableProperty] private string _bestEverStreakText = "0 Day Streak";
    public static bool ShowBackgroundSwirl => MetaDataManager.Data.ShowBackgroundSwirl;

    [NotifyPropertyChangedFor(nameof(IsSelectionModeActive))]
    [NotifyPropertyChangedFor(nameof(IsReviewSelectionMode))]
    [NotifyPropertyChangedFor(nameof(IsExportSelectionMode))]
    [NotifyPropertyChangedFor(nameof(CanShowDeckManagementActions))]
    [NotifyPropertyChangedFor(nameof(ReviewSelectionButtonText))]
    [NotifyPropertyChangedFor(nameof(ExportSelectionButtonText))]
    [ObservableProperty] private DeckSelectionMode _selectionMode = DeckSelectionMode.None;

    private readonly HashSet<ulong> _selectedDeckIds = [];
    public bool HasSelectedDecks => _selectedDeckIds.Count > 0;
    public int SelectedDeckCount => _selectedDeckIds.Count;

    public bool IsSelectionModeActive => SelectionMode != DeckSelectionMode.None;
    public bool IsReviewSelectionMode => SelectionMode == DeckSelectionMode.Review;
    public bool IsExportSelectionMode => SelectionMode == DeckSelectionMode.Export;

    public bool CanShowDeckManagementActions => !IsSelectionModeActive;
    public string ReviewSelectionButtonText => !IsReviewSelectionMode ?
        "Select Multiple" : HasSelectedDecks ?
        $"Play Selected ({SelectedDeckCount})" : "Cancel";
    public string ExportSelectionButtonText => !IsExportSelectionMode ?
        "Export" : HasSelectedDecks ?
        $"Export Selected ({SelectedDeckCount})" : "Cancel";

    [ObservableProperty] private bool _showGroups = true;
    partial void OnShowGroupsChanged(bool value) => RefreshDashboardItems();
    [ObservableProperty] private bool _showSets = true;
    partial void OnShowSetsChanged(bool value) => RefreshDashboardItems();
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private TimePeriodOption _selectedTimePeriod = null!;
    partial void OnSelectedTimePeriodChanged(TimePeriodOption value)
    {
        if (IsViewingDeckStats && !IsGraphView && SelectedDeckForStats != null) { ShowDeckStats(SelectedDeckForStats); }
        else if (IsViewingGroupStats && !IsGraphView && SelectedGroupForStats != null) { ShowGroupStats(SelectedGroupForStats); }
        else { LoadStats(); }
    }

    public ObservableCollection<string> GraphGroupingOptions { get; } = ["Daily", "Weekly", "Monthly"];

    [ObservableProperty] private string _selectedAttemptsGrouping = null!;
    partial void OnSelectedAttemptsGroupingChanged(string value) { if (IsGraphView) RefreshGraphStats(); }

    [ObservableProperty] private string _selectedTimeGrouping = null!;
    partial void OnSelectedTimeGroupingChanged(string value) { if (IsGraphView) RefreshGraphStats(); }

    [ObservableProperty] private int _totalQuestions = 0;
    [NotifyPropertyChangedFor(nameof(AverageTimePerCardText))]
    [ObservableProperty] private int _totalCardCount = 0;

    [ObservableProperty] private int _totalCorrect = 0;
    [ObservableProperty] private double _percentage = 0;
    [ObservableProperty] private string _grade = "U";

    [NotifyPropertyChangedFor(nameof(TotalTimeFormatted))]
    [NotifyPropertyChangedFor(nameof(AverageTimePerCardText))]
    [ObservableProperty] private int _totalTimeSeconds = 0;

    public string TotalTimeFormatted => TextUtility.FormatTime(TimeSpan.FromSeconds(TotalTimeSeconds));
    public string AverageTimePerCardText => TotalCardCount <= 0 ? "0.00" :
        TextUtility.FormatTime(TimeSpan.FromSeconds((int)Math.Round((double)TotalTimeSeconds / TotalCardCount)));


    [NotifyPropertyChangedFor(nameof(IsViewingStats))]
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private bool _isViewingDeckStats = false;
    [NotifyPropertyChangedFor(nameof(IsViewingStats))]
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private bool _isViewingGroupStats = false;
    public bool IsViewingStats => IsViewingDeckStats || IsViewingGroupStats;

    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private FlashCardDeck? _selectedDeckForStats = null;
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private StudyGroup? _selectedGroupForStats = null;

    public string CurrentStatsTitle =>
        IsViewingDeckStats ? SelectedDeckForStats?.Name ?? "Overall" :
        IsViewingGroupStats ? SelectedGroupForStats?.Name ?? "Overal" :
        "Overall";

    public string GraphViewTitle => $"{CurrentStatsTitle} Graph View";

    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private string _graphDateRangeText = "";

    public string GraphViewSubtitle => AttemptsGraphPoints.Count == 0
        ? ""
        : string.IsNullOrWhiteSpace(GraphDateRangeText)
            ? SelectedTimePeriod?.Label ?? "All Time"
            : GraphDateRangeText;

    public ObservableCollection<TimePeriodOption> TimePeriods { get; } = [
        new("All Time", null!), new("Last 6 Months", "-6 months"), new("Last 3 Months", "-3 months"),
        new("Last Month", "-1 months"), new("Last 2 Weeks", "-14 days"), new("Last Week", "-7 days"),
        new("Last 3 Days", "-3 days"), new("Last Day", "-1 days")
    ];

    public ObservableCollection<SortOption> SortOptions { get; } = [
        new("Name A-Z", SORT_NAME_ASC), new("Name Z-A", SORT_NAME_DESC), new("Cards (high → low)", SORT_CARDS_DESC),
        new("Cards (low → high)", SORT_CARDS_ASC), new("Study time (high → low)", SORT_STUDYTIME_DESC),
        new("Study time (low → high)", SORT_STUDYTIME_ASC)
    ];

    [NotifyPropertyChangedFor(nameof(HasGraphData))]
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _attemptsGraphPoints = [];
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _timeGraphPoints = [];
    public ObservableCollection<FlashCardDeck> Decks { get; } = [];
    public ObservableCollection<StudyGroup> StudyGroups { get; } = [];
    public ObservableCollection<object> DashboardItems { get; } = [];
    public ObservableCollection<FlashCardDeck> FilteredDecks { get; } = [];
    public bool HasGraphData => AttemptsGraphPoints.Count > 0;

    [ObservableProperty] private SortOption? _selectedSortOption = null;
    partial void OnSelectedSortOptionChanged(SortOption? value) => FilterDecks();

    public DashboardViewModel()
    {
        MetaDataManager.Data.PropertyChanged += Settings_PropertyChanged;
        RefreshStreakTexts();
        CurrentPage = this;

        SelectedTimePeriod = TimePeriods[0];
        SelectedAttemptsGrouping = GraphGroupingOptions[0];
        SelectedTimeGrouping = GraphGroupingOptions[0];
        SelectedSortOption = SortOptions[0];

        LoadDecksFromDatabase();
        FilterDecks();
        RefreshDashboardItems();
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppMetaData.ShowBackgroundSwirl):
                OnPropertyChanged(nameof(ShowBackgroundSwirl));
                break;
            case nameof(AppMetaData.LaunchStreak):
            case nameof(AppMetaData.BestLaunchStreak):
                RefreshStreakTexts();
                break;
        }
    }

    private void RefreshStreakTexts()
    {
        StreakText = $"{MetaDataManager.Data.LaunchStreak} Day Streak";
        BestEverStreakText = $"{MetaDataManager.Data.BestLaunchStreak} Days";
    }

    private void LoadStats()
    {
        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(null, timeModifier);

        TotalQuestions = total;
        TotalCorrect = correct;
        TotalTimeSeconds = timeTakenSeconds;
        TotalCardCount = FlashCardRepository.GetCardCount();
        BestAnswerStreakText = "0";
        Percentage = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;
        Grade = CalculateGradeWithDefault(correct, total);
        RefreshGraphStats();
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
        IsGraphView = false;
        SelectedDeckForStats = deck;
        IsViewingDeckStats = true;
        IsViewingGroupStats = false;

        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total, timeTakenSeconds, percentage, grade) = GetDeckStats(deck.ID, timeModifier);
        TotalQuestions = total;
        TotalCorrect = correct;
        TotalTimeSeconds = timeTakenSeconds;
        TotalCardCount = deck.CardCount;
        BestAnswerStreakText = FlashCardRepository.GetBestAnswerStreak("Deck", deck.ID).ToString();
        Percentage = percentage;
        Grade = grade;
        RefreshGraphStats();
    }

    public void ShowGroupStats(StudyGroup group)
    {
        IsGraphView = false;
        SelectedGroupForStats = group;
        IsViewingGroupStats = true;
        IsViewingDeckStats = false;

        var timeModifier = SelectedTimePeriod?.TimeModifier;

        // Get all decks in the group and sum their stats
        var decksInGroup = FlashCardRepository.GetDecksForStudyGroup(group.ID);

        int totalCorrect = 0;
        int totalQuestions = 0;
        int totalSeconds = 0;
        int totalCards = 0;

        foreach (var deck in decksInGroup)
        {
            var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(deck.ID, timeModifier);
            totalCorrect += correct;
            totalQuestions += total;
            totalSeconds += timeTakenSeconds;
            totalCards += deck.CardCount;
        }

        double percentage = totalQuestions > 0 ? Math.Round((double)totalCorrect / totalQuestions * 100, 1) : 0;
        string grade = CalculateGradeWithDefault(totalCorrect, totalQuestions);

        TotalQuestions = totalQuestions;
        TotalCorrect = totalCorrect;
        TotalTimeSeconds = totalSeconds;
        TotalCardCount = totalCards;
        BestAnswerStreakText = FlashCardRepository.GetBestAnswerStreak("Group", group.ID).ToString();
        Percentage = percentage;
        Grade = grade;
        RefreshGraphStats();
    }

    public void ShowOverallStats()
    {
        IsGraphView = false;
        IsViewingDeckStats = false;
        IsViewingGroupStats = false;
        SelectedDeckForStats = null;
        SelectedGroupForStats = null;
        LoadStats();
    }

    public void EnterGraphView()
    {
        IsGraphView = true;
        RefreshGraphStats();
    }

    public void ExitGraphView()
    {
        IsGraphView = false;
    }

    private StatsScope GetCurrentStatsScope()
    {
        if (IsViewingDeckStats && SelectedDeckForStats != null)
        {
            return StatsScope.Deck;
        }

        if (IsViewingGroupStats && SelectedGroupForStats != null)
        {
            return StatsScope.Group;
        }

        return StatsScope.Overall;
    }

    private void RefreshGraphStats()
    {
        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var rawRows = GetDailyStatsForCurrentScope(timeModifier);

        // Discard anomalies where attempts are 0
        var validRows = rawRows.Where(r => r.total > 0).ToList();

        // Attempts Grouping
        var attemptsRows = GroupRows(validRows, SelectedAttemptsGrouping).ToList();
        int maxAttempts = attemptsRows.Count == 0 ? 0 : attemptsRows.Max(row => row.total);

        // Time Grouping
        var timeRows = GroupRows(validRows, SelectedTimeGrouping).ToList();
        int maxTime = timeRows.Count == 0 ? 0 : timeRows.Max(row => row.timeTakenSeconds);

        AttemptsGraphPoints = new ObservableCollection<GraphStatPointViewModel>(
            attemptsRows.Select(row => new GraphStatPointViewModel(
                row.label,
                row.correct,
                row.total,
                row.timeTakenSeconds,
                maxAttempts,
                0))); // maxTime not relevant for attempts chart

        TimeGraphPoints = new ObservableCollection<GraphStatPointViewModel>(
            timeRows.Select(row => new GraphStatPointViewModel(
                row.label,
                row.correct,
                row.total,
                row.timeTakenSeconds,
                0, // maxAttempts not relevant for time chart
                maxTime)));

        GraphDateRangeText = validRows.Count == 0
            ? "No study history recorded yet."
            : $"{validRows.First().date:MMM d, yyyy} - {validRows.Last().date:MMM d, yyyy}";
    }

    private IEnumerable<(DateOnly date, int correct, int total, int timeTakenSeconds, string label)> GroupRows(
        List<(DateOnly date, int correct, int total, int timeTakenSeconds)> validRows, string? groupingValue)
    {
        if (groupingValue == "Weekly")
        {
            return validRows.GroupBy(r =>
                {
                    int diff = (7 + (r.date.DayOfWeek - DayOfWeek.Monday)) % 7;
                    return r.date.AddDays(-1 * diff);
                })
                .Select(g => (
                    date: g.Key,
                    correct: g.Sum(x => x.correct),
                    total: g.Sum(x => x.total),
                    timeTakenSeconds: g.Sum(x => x.timeTakenSeconds),
                    label: g.Key.ToString("MMM d")
                )).OrderBy(r => r.date);
        }
        else if (groupingValue == "Monthly")
        {
            return validRows.GroupBy(r => new { r.date.Year, r.date.Month })
                .Select(g => (
                    date: new DateOnly(g.Key.Year, g.Key.Month, 1),
                    correct: g.Sum(x => x.correct),
                    total: g.Sum(x => x.total),
                    timeTakenSeconds: g.Sum(x => x.timeTakenSeconds),
                    label: new DateOnly(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy")
                )).OrderBy(r => r.date);
        }
        else
        {
            return validRows.Select(r => (r.date, r.correct, r.total, r.timeTakenSeconds, label: r.date.ToString("MMM d")));
        }
    }

    private List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetDailyStatsForCurrentScope(string? timeModifier)
    {
        return GetCurrentStatsScope() switch
        {
            StatsScope.Deck when SelectedDeckForStats != null => FlashCardRepository.GetStatsByDate(SelectedDeckForStats.ID, timeModifier),
            StatsScope.Group when SelectedGroupForStats != null => GetDailyStatsForGroup(SelectedGroupForStats.ID, timeModifier),
            _ => FlashCardRepository.GetStatsByDate(null, timeModifier)
        };
    }

    private static List<(DateOnly date, int correct, int total, int timeTakenSeconds)> GetDailyStatsForGroup(ulong groupId, string? timeModifier)
    {
        var decksInGroup = FlashCardRepository.GetDecksForStudyGroup(groupId);
        var totalsByDate = new SortedDictionary<DateOnly, (int correct, int total, int timeTakenSeconds)>();

        foreach (var deck in decksInGroup)
        {
            foreach (var row in FlashCardRepository.GetStatsByDate(deck.ID, timeModifier))
            {
                if (totalsByDate.TryGetValue(row.date, out var existing))
                {
                    totalsByDate[row.date] = (
                        existing.correct + row.correct,
                        existing.total + row.total,
                        existing.timeTakenSeconds + row.timeTakenSeconds);
                }
                else
                {
                    totalsByDate[row.date] = (row.correct, row.total, row.timeTakenSeconds);
                }
            }
        }

        return totalsByDate
            .Select(entry => (entry.Key, entry.Value.correct, entry.Value.total, entry.Value.timeTakenSeconds))
            .ToList();
    }

    public (int correct, int total, int timeTakenSeconds, double percentage, string grade) GetDeckStats(ulong deckID, string? timeModifier = null)
    {
        var (correct, total, timeTakenSeconds) = FlashCardRepository.GetStats(deckID, timeModifier);
        double percentage = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;

        string grade = CalculateGradeWithDefault(correct, total);

        return (correct, total, timeTakenSeconds, percentage, grade);
    }

    public void FilterDecks()
    {
        FilteredDecks.Clear();

        List<FlashCardDeck> resultsList = [.. Decks.FilterBySearch(SearchText)];

        if (SelectedSortOption != null)
        {
            switch (SelectedSortOption.Key)
            {
                case SORT_NAME_ASC:
                    resultsList = [.. resultsList.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)];
                    break;
                case SORT_NAME_DESC:
                    resultsList = [.. resultsList.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)];
                    break;
                case SORT_CARDS_ASC:
                    resultsList = [.. resultsList.OrderBy(d => d.CardCount)];
                    break;
                case SORT_CARDS_DESC:
                    resultsList = [.. resultsList.OrderByDescending(d => d.CardCount)];
                    break;
                case SORT_STUDYTIME_ASC:
                    resultsList = [.. resultsList.OrderBy(d => FlashCardRepository.GetStats(d.ID).timeTakenSeconds)];
                    break;
                case SORT_STUDYTIME_DESC:
                    resultsList = [.. resultsList.OrderByDescending(d => FlashCardRepository.GetStats(d.ID).timeTakenSeconds)];
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
        IsGraphView = false;
        RefreshStreakTexts();
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
            IEnumerable<StudyGroup> groupsToAdd = StudyGroups;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                groupsToAdd = groupsToAdd.Where(g => g.Name.ToLower().Contains(lowerSearch));
            }

            if (SelectedSortOption != null)
            {
                var timeModifier = SelectedTimePeriod?.TimeModifier;

                groupsToAdd = SelectedSortOption.Key switch
                {
                    SORT_NAME_ASC => groupsToAdd.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase),
                    SORT_NAME_DESC => groupsToAdd.OrderByDescending(g => g.Name, StringComparer.OrdinalIgnoreCase),
                    SORT_CARDS_ASC => groupsToAdd.OrderBy(g => g.CardCount),
                    SORT_CARDS_DESC => groupsToAdd.OrderByDescending(g => g.CardCount),
                    SORT_STUDYTIME_ASC => groupsToAdd.OrderBy(g => GetGroupStudyTimeSeconds(g, timeModifier)),
                    SORT_STUDYTIME_DESC => groupsToAdd.OrderByDescending(g => GetGroupStudyTimeSeconds(g, timeModifier)),
                    _ => groupsToAdd
                };
            }

            foreach (var group in groupsToAdd)
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

    private static int GetGroupStudyTimeSeconds(StudyGroup group, string? timeModifier)
    {
        var decksInGroup = FlashCardRepository.GetDecksForStudyGroup(group.ID);
        int totalSeconds = 0;

        foreach (var deck in decksInGroup)
        {
            totalSeconds += FlashCardRepository.GetStats(deck.ID, timeModifier).timeTakenSeconds;
        }

        return totalSeconds;
    }
}