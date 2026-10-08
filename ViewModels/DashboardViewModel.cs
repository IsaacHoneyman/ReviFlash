using System.Collections.ObjectModel;
using ReviFlash.Models;
using ReviFlash.Data.Local;
using System.Linq;
using System;
using System.Collections.Generic;

using static ReviFlash.Utilities.CardUtility;
using CommunityToolkit.Mvvm.ComponentModel;
using ReviFlash.Utilities;
using ReviFlash.Data.Online;

namespace ReviFlash.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private enum StatsScope { Overall, Deck, Group, Note, }
    public enum DeckSelectionMode { None, Review, }

    // Ordering within a mixed list: folders, then groups, then sets, then notes.
    private const int FolderPriority = 0;
    private const int GroupPriority = 1;
    private const int SetPriority = 2;
    private const int NotePriority = 3;

    public class TimePeriodOption(string label, string timeModifier)
    {
        public string Label { get; set; } = label;
        public string TimeModifier { get; set; } = timeModifier;
    }

    [ObservableProperty] private object _currentPage = new();
    [ObservableProperty] private string _bestAnswerStreakText = "0";
    [ObservableProperty] private bool _isGraphView;
    [NotifyPropertyChangedFor(nameof(IsSearching))]
    [ObservableProperty] private string _searchText = "";
    partial void OnSearchTextChanged(string value) => RefreshLibraryView();
    [ObservableProperty] private string _streakText = "0 Day Streak";
    [ObservableProperty] private string _bestEverStreakText = "0 Day Streak";

    // --- Account ---

    public bool IsSignedIn => AuthSession.IsSignedIn;
    public string AccountText => IsSignedIn ? $"Welcome, {AuthSession.Username}" : "Guest";

    [NotifyPropertyChangedFor(nameof(IsSelectionModeActive))]
    [NotifyPropertyChangedFor(nameof(IsReviewSelectionMode))]
    [NotifyPropertyChangedFor(nameof(CanShowDeckManagementActions))]
    [NotifyPropertyChangedFor(nameof(ReviewSelectionButtonText))]
    [ObservableProperty] private DeckSelectionMode _selectionMode = DeckSelectionMode.None;

    private readonly HashSet<ulong> _selectedDeckIds = [];
    public bool HasSelectedDecks => _selectedDeckIds.Count > 0;
    public int SelectedDeckCount => _selectedDeckIds.Count;

    public bool IsSelectionModeActive => SelectionMode != DeckSelectionMode.None;
    public bool IsReviewSelectionMode => SelectionMode == DeckSelectionMode.Review;

    public bool CanShowDeckManagementActions => !IsSelectionModeActive;
    public string ReviewSelectionButtonText => !IsReviewSelectionMode ?
        "Select Multiple" : HasSelectedDecks ?
        $"Play Selected ({SelectedDeckCount})" : "Cancel";

    [ObservableProperty] private bool _showGroups = true;
    partial void OnShowGroupsChanged(bool value) => RefreshLibraryView();
    [ObservableProperty] private bool _showSets = true;
    partial void OnShowSetsChanged(bool value) => RefreshLibraryView();
    [ObservableProperty] private bool _showFolders = true;
    partial void OnShowFoldersChanged(bool value) => RefreshLibraryView();
    [ObservableProperty] private bool _showNotes = true;
    partial void OnShowNotesChanged(bool value) => RefreshLibraryView();

    // --- Folders ---

    private FolderTree _folderTree = new([]);
    private bool _suspendRefresh;

    /// <summary> The folder currently open, or null at the main menu. </summary>
    [NotifyPropertyChangedFor(nameof(IsInFolder))]
    [NotifyPropertyChangedFor(nameof(CurrentFolderName))]
    [NotifyPropertyChangedFor(nameof(SearchWatermark))]
    [ObservableProperty] private ulong? _currentFolderID;

    public ObservableCollection<Folder> Breadcrumbs { get; } = [];

    public FolderTree FolderTree => _folderTree;

    public bool IsInFolder => CurrentFolderID.HasValue;
    public string CurrentFolderName => _folderTree.Get(CurrentFolderID)?.Name ?? FolderTree.RootLabel;
    public bool IsSearching => !SearchUtility.IsEmptyQuery(SearchText);

    public string SearchWatermark => IsInFolder
        ? $"Search in {CurrentFolderName} and its folders..."
        : "Search sets, notes, groups and folders...";

    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    [ObservableProperty] private string _emptyStateText = "";
    public bool HasNoItems => DashboardItems.Count == 0;
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private TimePeriodOption _selectedTimePeriod = null!;
    partial void OnSelectedTimePeriodChanged(TimePeriodOption value)
    {
        // Reload whatever is being viewed for the new period, staying in the graph view if that's where it changed.
        var inGraphView = IsGraphView;
        RefreshStats();
        IsGraphView = inGraphView;
    }

    public ObservableCollection<string> GraphGroupingOptions { get; } = ["Daily", "Weekly", "Monthly"];

    [ObservableProperty] private string _selectedAttemptsGrouping = null!;
    partial void OnSelectedAttemptsGroupingChanged(string value) { if (IsGraphView) RefreshGraphStats(); }

    [ObservableProperty] private string _selectedTimeGrouping = null!;
    partial void OnSelectedTimeGroupingChanged(string value) { if (IsGraphView) RefreshGraphStats(); }

    [ObservableProperty] private int _totalQuestions = 0;
    [ObservableProperty] private int _totalCardCount = 0;

    [ObservableProperty] private int _totalCorrect = 0;
    [ObservableProperty] private double _percentage = 0;
    [ObservableProperty] private string _grade = "U";

    /// <summary> Time spent reviewing flashcards (in the set, group or everything being viewed). </summary>
    [NotifyPropertyChangedFor(nameof(TotalTimeFormatted))]
    [NotifyPropertyChangedFor(nameof(FlashcardTimeFormatted))]
    [ObservableProperty] private int _totalTimeSeconds = 0;

    /// <summary> Time spent in notes (in the note, or all notes, being viewed). </summary>
    [NotifyPropertyChangedFor(nameof(TotalTimeFormatted))]
    [NotifyPropertyChangedFor(nameof(NoteTimeFormatted))]
    [ObservableProperty] private int _noteTimeSeconds = 0;

    public string TotalTimeFormatted => TextUtility.FormatTime(TimeSpan.FromSeconds(TotalTimeSeconds + NoteTimeSeconds));
    public string FlashcardTimeFormatted => TextUtility.FormatTime(TimeSpan.FromSeconds(TotalTimeSeconds));
    public string NoteTimeFormatted => TextUtility.FormatTime(TimeSpan.FromSeconds(NoteTimeSeconds));

    [NotifyPropertyChangedFor(nameof(IsViewingStats))]
    [NotifyPropertyChangedFor(nameof(IsViewingCardStats))]
    [NotifyPropertyChangedFor(nameof(ShowTimeSplit))]
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private bool _isViewingDeckStats = false;
    [NotifyPropertyChangedFor(nameof(IsViewingStats))]
    [NotifyPropertyChangedFor(nameof(IsViewingCardStats))]
    [NotifyPropertyChangedFor(nameof(ShowTimeSplit))]
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private bool _isViewingGroupStats = false;
    [NotifyPropertyChangedFor(nameof(IsViewingStats))]
    [NotifyPropertyChangedFor(nameof(ShowTimeSplit))]
    [NotifyPropertyChangedFor(nameof(ShowCardStats))]
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private bool _isViewingNoteStats = false;

    public bool IsViewingStats => IsViewingDeckStats || IsViewingGroupStats || IsViewingNoteStats;
    /// <summary> A set or group: the stats that only flashcards have, like the best answer streak. </summary>
    public bool IsViewingCardStats => IsViewingDeckStats || IsViewingGroupStats;
    /// <summary> Questions, accuracy and grade: everywhere except a note's stats. </summary>
    public bool ShowCardStats => !IsViewingNoteStats;
    /// <summary> Flashcard and notes time shown separately: only in the overall stats. </summary>
    public bool ShowTimeSplit => !IsViewingStats;

    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private FlashCardDeck? _selectedDeckForStats = null;
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private StudyGroup? _selectedGroupForStats = null;
    [NotifyPropertyChangedFor(nameof(CurrentStatsTitle))]
    [NotifyPropertyChangedFor(nameof(GraphViewTitle))]
    [ObservableProperty] private Note? _selectedNoteForStats = null;

    [ObservableProperty] private string _noteWordsText = "";
    [ObservableProperty] private string _noteHeadingsText = "";
    [ObservableProperty] private string _noteLastStudiedText = "";

    public string CurrentStatsTitle =>
        IsViewingDeckStats ? SelectedDeckForStats?.Name ?? "Overall" :
        IsViewingGroupStats ? SelectedGroupForStats?.Name ?? "Overall" :
        IsViewingNoteStats ? SelectedNoteForStats?.Name ?? "Overall" :
        "Overall";

    public string GraphViewTitle => $"{CurrentStatsTitle} Graph View";

    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private string _graphDateRangeText = "";

    public string GraphViewSubtitle => !HasGraphData
        ? ""
        : string.IsNullOrWhiteSpace(GraphDateRangeText)
            ? SelectedTimePeriod?.Label ?? "All Time"
            : GraphDateRangeText;

    // --- Study calendar and where the time went (graph view) ---

    public ObservableCollection<HeatmapWeek> HeatmapWeeks { get; } = [];
    [ObservableProperty] private string _heatmapSummary = "";

    public ObservableCollection<TimeSpentItem> TimeBreakdown { get; } = [];
    public bool HasTimeBreakdown => TimeBreakdown.Count > 0;

    public ObservableCollection<TimePeriodOption> TimePeriods { get; } = [
        new("All Time", null!), new("Last 6 Months", "-6 months"), new("Last 3 Months", "-3 months"),
        new("Last Month", "-1 months"), new("Last 2 Weeks", "-14 days"), new("Last Week", "-7 days"),
        new("Last 3 Days", "-3 days"), new("Last Day", "-1 days")
    ];

    public ObservableCollection<SortOption> SortOptions { get; } = [.. SearchUtility.SortOptions];

    [NotifyPropertyChangedFor(nameof(HasGraphData))]
    [NotifyPropertyChangedFor(nameof(HasAttemptsData))]
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _attemptsGraphPoints = [];
    [NotifyPropertyChangedFor(nameof(HasGraphData))]
    [NotifyPropertyChangedFor(nameof(HasTimeData))]
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _timeGraphPoints = [];
    public ObservableCollection<FlashCardDeck> Decks { get; } = [];
    public ObservableCollection<StudyGroup> StudyGroups { get; } = [];
    public ObservableCollection<Note> Notes { get; } = [];
    public ObservableCollection<object> DashboardItems { get; } = [];
    public bool HasAttemptsData => AttemptsGraphPoints.Count > 0;
    public bool HasTimeData => TimeGraphPoints.Count > 0;
    public bool HasGraphData => HasAttemptsData || HasTimeData;

    [ObservableProperty] private SortOption? _selectedSortOption = null;
    partial void OnSelectedSortOptionChanged(SortOption? value) => RefreshLibraryView();

    public DashboardViewModel()
    {
        MetaDataManager.Data.PropertyChanged += Settings_PropertyChanged;
        RefreshStreakTexts();
        CurrentPage = this;

        SelectedTimePeriod = TimePeriods[0];
        SelectedAttemptsGrouping = GraphGroupingOptions[0];
        SelectedTimeGrouping = GraphGroupingOptions[0];
        SelectedSortOption = SortOptions[0];

        ReloadLibrary();
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppMetaData.LaunchStreak):
            case nameof(AppMetaData.BestLaunchStreak):
                RefreshStreakTexts();
                break;
            // Signing in or out anywhere (the online windows, a refresh being rejected) lands here.
            case nameof(AppMetaData.SupabaseRefreshToken):
            case nameof(AppMetaData.SupabaseAccessToken):
            case nameof(AppMetaData.SupabaseUsername):
            case nameof(AppMetaData.SupabaseExpirationTime):
                OnPropertyChanged(nameof(IsSignedIn));
                OnPropertyChanged(nameof(AccountText));
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
        NoteTimeSeconds = NoteRepository.GetStudySeconds(null, timeModifier);
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
        else if (IsViewingNoteStats && SelectedNoteForStats != null)
        {
            ShowNoteStats(SelectedNoteForStats);
        }
        else
        {
            LoadStats();
        }
    }

    public void ShowNoteStats(Note note)
    {
        IsGraphView = false;
        SelectedNoteForStats = note;
        IsViewingNoteStats = true;
        IsViewingDeckStats = false;
        IsViewingGroupStats = false;

        var content = NoteRepository.GetContent(note.ID);
        TotalQuestions = 0;
        TotalCorrect = 0;
        Percentage = 0;
        Grade = "-";
        TotalTimeSeconds = 0;
        NoteTimeSeconds = NoteRepository.GetStudySeconds(note.ID, SelectedTimePeriod?.TimeModifier);
        NoteWordsText = NoteText.WordCount(content).ToString("N0");
        NoteHeadingsText = NoteText.Headings(content).Count.ToString();
        NoteLastStudiedText = NoteRepository.GetLastStudied(note.ID) is { } day ? FormatDay(day) : "Never";
        RefreshGraphStats();
    }

    private static string FormatDay(DateOnly day)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (day == today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";
        return day.Year == today.Year ? day.ToString("d MMM") : day.ToString("d MMM yyyy");
    }

    public void ShowDeckStats(FlashCardDeck deck)
    {
        IsGraphView = false;
        SelectedDeckForStats = deck;
        IsViewingDeckStats = true;
        IsViewingGroupStats = false;
        IsViewingNoteStats = false;

        var timeModifier = SelectedTimePeriod?.TimeModifier;
        var (correct, total, timeTakenSeconds, percentage, grade) = GetDeckStats(deck.ID, timeModifier);
        TotalQuestions = total;
        TotalCorrect = correct;
        TotalTimeSeconds = timeTakenSeconds;
        NoteTimeSeconds = 0;
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
        IsViewingNoteStats = false;
        NoteTimeSeconds = 0;

        var timeModifier = SelectedTimePeriod?.TimeModifier;

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

    /// <summary> After a delete: stats for a set, group or note that's gone go back to overall, and the rest reload. </summary>
    private void RefreshStatsAfterDelete()
    {
        if ((IsViewingDeckStats && SelectedDeckForStats is { } deck && Decks.All(d => d.ID != deck.ID))
            || (IsViewingGroupStats && SelectedGroupForStats is { } group && StudyGroups.All(g => g.ID != group.ID))
            || (IsViewingNoteStats && SelectedNoteForStats is { } note && Notes.All(n => n.ID != note.ID)))
            ShowOverallStats();
        else
            RefreshStats();
    }

    public void ShowOverallStats()
    {
        IsGraphView = false;
        IsViewingDeckStats = false;
        IsViewingGroupStats = false;
        IsViewingNoteStats = false;
        SelectedDeckForStats = null;
        SelectedGroupForStats = null;
        SelectedNoteForStats = null;
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

        if (IsViewingNoteStats && SelectedNoteForStats != null)
        {
            return StatsScope.Note;
        }

        return StatsScope.Overall;
    }

    private void RefreshGraphStats()
    {
        var timeModifier = SelectedTimePeriod?.TimeModifier;

        // Discard anomalies where attempts are 0
        var validRows = GetDailyStatsForCurrentScope(timeModifier).Where(r => r.total > 0).ToList();
        var noteRows = GetDailyNoteTimeForCurrentScope(timeModifier);

        var attemptsRows = GroupRows(validRows, SelectedAttemptsGrouping).ToList();
        int maxAttempts = attemptsRows.Count == 0 ? 0 : attemptsRows.Max(row => row.total);

        AttemptsGraphPoints = new ObservableCollection<GraphStatPointViewModel>(
            attemptsRows.Select(row => new GraphStatPointViewModel(
                row.label,
                row.correct,
                row.total,
                row.timeTakenSeconds,
                maxAttempts,
                0))); // maxTime not relevant for attempts chart

        // Study time: flashcards and notes by day, then grouped, so each bar can show both.
        var timeByDay = new SortedDictionary<DateOnly, (int cards, int notes)>();
        foreach (var row in validRows) timeByDay[row.date] = (row.timeTakenSeconds, 0);
        foreach (var (date, seconds) in noteRows)
            timeByDay[date] = timeByDay.TryGetValue(date, out var day) ? (day.cards, day.notes + seconds) : (0, seconds);

        var timeRows = timeByDay
            .Where(day => day.Value.cards + day.Value.notes > 0)
            .GroupBy(day => Period(day.Key, SelectedTimeGrouping))
            .OrderBy(group => group.Key.Start)
            .Select(group => (group.Key.Label, cards: group.Sum(day => day.Value.cards), notes: group.Sum(day => day.Value.notes)))
            .ToList();
        int maxTime = timeRows.Count == 0 ? 0 : timeRows.Max(row => row.cards + row.notes);

        TimeGraphPoints = new ObservableCollection<GraphStatPointViewModel>(
            timeRows.Select(row => new GraphStatPointViewModel(row.Label, 0, 0, row.cards, 0, maxTime, row.notes)));

        GraphDateRangeText = timeByDay.Count == 0
            ? "No study history recorded yet."
            : $"{timeByDay.Keys.First():MMM d, yyyy} - {timeByDay.Keys.Last():MMM d, yyyy}";

        RefreshHeatmap();
        RefreshTimeBreakdown(timeModifier);
    }

    /// <summary> The day, week (from Monday) or month a date falls in, with its chart label. </summary>
    private static (DateOnly Start, string Label) Period(DateOnly date, string? grouping)
    {
        if (grouping == "Weekly")
        {
            var monday = date.AddDays(-((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7));
            return (monday, monday.ToString("MMM d"));
        }
        if (grouping == "Monthly")
        {
            var first = new DateOnly(date.Year, date.Month, 1);
            return (first, first.ToString("MMM yyyy"));
        }
        return (date, date.ToString("MMM d"));
    }

    /// <summary> Time spent in notes per day: all notes overall, one note in its stats, none for a set or group. </summary>
    private List<(DateOnly date, int seconds)> GetDailyNoteTimeForCurrentScope(string? timeModifier) => GetCurrentStatsScope() switch
    {
        StatsScope.Overall => NoteRepository.GetStudyTimeByDate(null, timeModifier),
        StatsScope.Note when SelectedNoteForStats != null => NoteRepository.GetStudyTimeByDate(SelectedNoteForStats.ID, timeModifier),
        _ => [],
    };

    /// <summary> The study calendar: the last 53 weeks (Monday to Sunday), each day shaded by total study time, whatever period is chosen. </summary>
    private void RefreshHeatmap()
    {
        const string lastYear = "-371 days";

        var secondsByDay = new Dictionary<DateOnly, int>();
        foreach (var row in GetDailyStatsForCurrentScope(lastYear).Where(r => r.total > 0))
            secondsByDay[row.date] = secondsByDay.GetValueOrDefault(row.date) + row.timeTakenSeconds;
        foreach (var (date, seconds) in GetDailyNoteTimeForCurrentScope(lastYear))
            secondsByDay[date] = secondsByDay.GetValueOrDefault(date) + seconds;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var thisMonday = today.AddDays(-((7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7));
        var firstMonday = thisMonday.AddDays(-7 * 52);

        HeatmapWeeks.Clear();
        int totalSeconds = 0, activeDays = 0;
        for (var week = 0; week <= 52; week++)
        {
            var monday = firstMonday.AddDays(7 * week);
            var days = new List<HeatmapDay>();
            for (var weekday = 0; weekday < 7; weekday++)
            {
                var date = monday.AddDays(weekday);
                var seconds = date > today ? 0 : secondsByDay.GetValueOrDefault(date);
                if (seconds > 0) { totalSeconds += seconds; activeDays++; }
                days.Add(new HeatmapDay(date, seconds, date > today));
            }

            // A month's name over the first week that starts in it.
            var label = week == 0 || monday.Month != monday.AddDays(-7).Month ? monday.ToString("MMM") : "";
            HeatmapWeeks.Add(new HeatmapWeek(label, days));
        }

        HeatmapSummary = activeDays == 0
            ? "Nothing studied in the last year yet."
            : $"{TextUtility.FormatTime(TimeSpan.FromSeconds(totalSeconds))} over {activeDays} {(activeDays == 1 ? "day" : "days")} in the last year";
    }

    /// <summary> The sets and notes with the most time spent in the chosen period (overall stats only). </summary>
    private void RefreshTimeBreakdown(string? timeModifier)
    {
        TimeBreakdown.Clear();

        if (GetCurrentStatsScope() == StatsScope.Overall)
        {
            var deckNames = Decks.ToDictionary(deck => deck.ID, deck => deck.Name);
            var noteNames = Notes.ToDictionary(note => note.ID, note => note.Name);

            var items = FlashCardRepository.GetStudyTimeByDeck(timeModifier)
                .Where(row => row.seconds > 0 && deckNames.ContainsKey(row.deckID))
                .Select(row => (name: deckNames[row.deckID], kind: "Set", row.seconds))
                .Concat(NoteRepository.GetStudyTimeByNote(timeModifier)
                    .Where(row => row.seconds > 0 && noteNames.ContainsKey(row.noteID))
                    .Select(row => (name: noteNames[row.noteID], kind: "Note", row.seconds)))
                .OrderByDescending(item => item.seconds)
                .Take(10)
                .ToList();

            var most = items.Count == 0 ? 0 : items[0].seconds;
            foreach (var item in items) TimeBreakdown.Add(new TimeSpentItem(item.name, item.kind, item.seconds, most));
        }

        OnPropertyChanged(nameof(HasTimeBreakdown));
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
            StatsScope.Note => [],
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

    /// <summary> Rebuilds the visible list from the open folder; while searching it also covers the folder's subfolders. </summary>
    public void RefreshLibraryView()
    {
        if (_suspendRefresh) return;

        // The open folder can disappear (deleted, or replaced by a restored backup); fall back to the main menu.
        if (!_folderTree.Exists(CurrentFolderID))
        {
            CurrentFolderID = null;
            RefreshFolderMetadata();
        }

        var subtree = CurrentFolderID is ulong currentId ? _folderTree.SubtreeIds(currentId) : null;
        bool InScope(ulong? folderID) => subtree is null || (folderID is ulong id && subtree.Contains(id));

        var visible = new List<ISearchable>();

        if (ShowFolders)
        {
            visible.AddRange(IsSearching
                ? _folderTree.AllFolders.Where(folder => folder.ID != CurrentFolderID && InScope(folder.ParentFolderID))
                : _folderTree.ChildrenOf(CurrentFolderID));
        }

        if (ShowGroups)
        {
            visible.AddRange(IsSearching
                ? StudyGroups.Where(group => InScope(group.FolderID))
                : StudyGroups.Where(group => group.FolderID == CurrentFolderID));
        }

        if (ShowSets)
        {
            visible.AddRange(IsSearching
                ? Decks.Where(deck => InScope(deck.FolderID))
                : Decks.Where(deck => deck.FolderID == CurrentFolderID));
        }

        if (ShowNotes)
        {
            visible.AddRange(IsSearching
                ? Notes.Where(note => InScope(note.FolderID))
                : Notes.Where(note => note.FolderID == CurrentFolderID));
        }

        var ordered = visible.SearchAndSort(SearchText, SelectedSortOption?.Mode ?? SortMode.Relevance, TypePriority);

        foreach (var deck in ordered.OfType<FlashCardDeck>())
        {
            deck.IsSelectedForMultiReview = _selectedDeckIds.Contains(deck.ID);
        }

        DashboardItems.SyncTo<object>(ordered);

        RefreshEmptyState();
    }

    private static int TypePriority(ISearchable item) => item switch
    {
        Folder => FolderPriority,
        StudyGroup => GroupPriority,
        Note => NotePriority,
        _ => SetPriority,
    };

    /// <summary> Folder counts, "found in" labels and breadcrumbs: only reloading or navigating changes these, so searching skips them. </summary>
    private void RefreshFolderMetadata()
    {
        _folderTree.ApplyCounts(Decks, StudyGroups, Notes);
        _folderTree.ApplyPaths(Decks, StudyGroups, CurrentFolderID, Notes);

        Breadcrumbs.Clear();
        foreach (var folder in _folderTree.AncestorChain(CurrentFolderID)) Breadcrumbs.Add(folder);

        OnPropertyChanged(nameof(IsInFolder));
        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(SearchWatermark));
    }

    private void RefreshEmptyState()
    {
        EmptyStateText =
            IsSearching && IsInFolder ? $"Nothing in {CurrentFolderName} matches '{SearchText.Trim()}'." :
            IsSearching ? $"Nothing matches '{SearchText.Trim()}'." :
            IsInFolder ? $"{CurrentFolderName} is empty. Create a set or note here, or move one in." :
            "No flashcard sets or notes yet. Create one to get started.";

        OnPropertyChanged(nameof(HasNoItems));
    }

    // --- Folder navigation ---

    public void OpenFolder(Folder folder) => NavigateToFolder(folder.ID);

    /// <summary> Moves to a folder (or the main menu when null), clearing the search as it goes. </summary>
    public void NavigateToFolder(ulong? folderID)
    {
        // Both assignments would each trigger a rebuild; batch them into one.
        _suspendRefresh = true;
        CurrentFolderID = folderID;
        SearchText = "";
        _suspendRefresh = false;

        RefreshFolderMetadata();
        RefreshLibraryView();
    }

    public void NavigateUp() => NavigateToFolder(_folderTree.Get(CurrentFolderID)?.ParentFolderID);

    // --- Folder management ---

    public Folder CreateFolder(string name)
    {
        var folder = FolderRepository.CreateFolder(name.Trim(), CurrentFolderID);
        ReloadLibrary();
        return folder;
    }

    public void RenameFolder(Folder folder, string newName)
    {
        FolderRepository.RenameFolder(folder.ID, newName.Trim());
        ReloadLibrary();
    }

    /// <summary> Deletes the folder, moving its contents up to its parent unless <paramref name="withContents"/> deletes them too. </summary>
    public void DeleteFolder(Folder folder, bool withContents = false)
    {
        if (withContents) FolderRepository.DeleteFolderAndContents(FolderTree.SubtreeIds(folder.ID));
        else FolderRepository.DeleteFolder(folder.ID);

        // Standing inside the folder that just went away (or, with its contents, anywhere under it), step up out of it.
        if (CurrentFolderID is { } current && (current == folder.ID || (withContents && FolderTree.IsInSubtree(current, folder.ID))))
            CurrentFolderID = folder.ParentFolderID;

        ReloadLibrary();
        if (withContents) RefreshStatsAfterDelete();
    }

    /// <summary> Refiles a set, note, group or folder. Returns false when the move is not allowed. </summary>
    public bool MoveItem(object item, ulong? targetFolderID)
    {
        try
        {
            switch (item)
            {
                case FlashCardDeck deck:
                    FolderRepository.MoveDeck(deck.ID, targetFolderID);
                    break;
                case StudyGroup group:
                    FolderRepository.MoveStudyGroup(group.ID, targetFolderID);
                    break;
                case Note note:
                    FolderRepository.MoveNote(note.ID, targetFolderID);
                    break;
                case Folder folder:
                    FolderRepository.MoveFolder(folder.ID, targetFolderID);
                    break;
                default:
                    return false;
            }
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError("Move rejected", ex);
            return false;
        }

        ReloadLibrary();
        return true;
    }

    // --- Selection ---

    public void BeginReviewSelection()
    {
        BeginSelectionMode(DeckSelectionMode.Review);
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
        RefreshLibraryView();
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
        RefreshLibraryView();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedDecks));
        OnPropertyChanged(nameof(SelectedDeckCount));
        OnPropertyChanged(nameof(ReviewSelectionButtonText));
    }

    // --- Loading ---

    public void ReloadLibrary()
    {
        _folderTree = FolderTree.Load();

        var savedDecks = FlashCardRepository.GetAllDecks();
        Decks.Clear();
        foreach (var deck in savedDecks) Decks.Add(deck);

        var savedGroups = FlashCardRepository.GetAllStudyGroups();
        StudyGroups.Clear();
        foreach (var group in savedGroups) StudyGroups.Add(group);

        var savedNotes = NoteRepository.GetAllNotes();
        Notes.Clear();
        foreach (var note in savedNotes) Notes.Add(note);

        // A folder we were standing in may be gone after a reload.
        if (!_folderTree.Exists(CurrentFolderID)) CurrentFolderID = null;

        RefreshFolderMetadata();
        RefreshLibraryView();
    }

    public void DeleteDeck(FlashCardDeck deckToDelete)
    {
        FlashCardRepository.DeleteDeck(deckToDelete.ID);
        _selectedDeckIds.Remove(deckToDelete.ID);
        NotifySelectionChanged();

        // Groups reload too: deleting a set changes the card counts of any group holding it.
        ReloadLibrary();
        RefreshStatsAfterDelete();
    }

    public void DeleteStudyGroup(StudyGroup groupToDelete)
    {
        FlashCardRepository.DeleteStudyGroup(groupToDelete.ID);
        ReloadLibrary();
        RefreshStatsAfterDelete();
    }

    public void RefreshAfterBackupRestore()
    {
        IsGraphView = false;
        RefreshStreakTexts();
        CancelSelectionMode();

        // The restored database has its own folders; start from the main menu.
        _suspendRefresh = true;
        CurrentFolderID = null;
        SearchText = "";
        _suspendRefresh = false;

        ReloadLibrary();
        RefreshStats();
    }

    // --- Notes ---

    public Note CreateNote(string name)
    {
        var note = NoteRepository.CreateNote(name.Trim(), CurrentFolderID);
        ReloadLibrary();
        return note;
    }

    public void RenameNote(Note note, string newName)
    {
        NoteRepository.RenameNote(note.ID, newName.Trim());
        ReloadLibrary();
    }

    public void DeleteNote(Note note)
    {
        NoteRepository.DeleteNote(note.ID);
        ReloadLibrary();
        RefreshStatsAfterDelete();
    }

    /// <summary> Opens a note as a page of its own; leaving it comes back here. </summary>
    public void OpenNote(Note note)
    {
        CurrentPage = new NoteViewModel(note, () =>
        {
            CurrentPage = this;
            ReloadLibrary();
            RefreshStats();
        });
    }

    public FlashCardDeck CreateNewDeck()
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        FlashCardRepository.SaveNewDeck(newDeck);

        if (CurrentFolderID is ulong folderID) FolderRepository.MoveDeck(newDeck.ID, folderID);

        ReloadLibrary();
        return newDeck;
    }
}
