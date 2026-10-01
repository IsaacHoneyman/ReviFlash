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
    private enum StatsScope { Overall, Deck, Group, }
    public enum DeckSelectionMode { None, Review, }

    // Ordering within a mixed list: folders, then groups, then sets.
    private const int FolderPriority = 0;
    private const int GroupPriority = 1;
    private const int SetPriority = 2;

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
    public static bool ShowBackgroundSwirl => MetaDataManager.Data.ShowBackgroundSwirl;

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

    // --- Folders ---

    private FolderTree _folderTree = new([]);
    private bool _suspendRefresh;

    /// <summary> The folder currently open, or null at the main menu. </summary>
    [NotifyPropertyChangedFor(nameof(IsInFolder))]
    [NotifyPropertyChangedFor(nameof(CurrentFolderName))]
    [NotifyPropertyChangedFor(nameof(SearchWatermark))]
    [ObservableProperty] private ulong? _currentFolderID;

    /// <summary> Trail from the main menu down to the open folder, for the breadcrumb bar. </summary>
    public ObservableCollection<Folder> Breadcrumbs { get; } = [];

    public FolderTree FolderTree => _folderTree;

    public bool IsInFolder => CurrentFolderID.HasValue;
    public string CurrentFolderName => _folderTree.Get(CurrentFolderID)?.Name ?? FolderTree.RootLabel;
    public bool IsSearching => !SearchUtility.IsEmptyQuery(SearchText);

    public string SearchWatermark => IsInFolder
        ? $"Search in {CurrentFolderName} and its folders..."
        : "Search sets, groups and folders...";

    [NotifyPropertyChangedFor(nameof(HasNoItems))]
    [ObservableProperty] private string _emptyStateText = "";
    public bool HasNoItems => DashboardItems.Count == 0;
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

    public ObservableCollection<SortOption> SortOptions { get; } = [.. SearchUtility.SortOptions];

    [NotifyPropertyChangedFor(nameof(HasGraphData))]
    [NotifyPropertyChangedFor(nameof(GraphViewSubtitle))]
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _attemptsGraphPoints = [];
    [ObservableProperty] private ObservableCollection<GraphStatPointViewModel> _timeGraphPoints = [];
    public ObservableCollection<FlashCardDeck> Decks { get; } = [];
    public ObservableCollection<StudyGroup> StudyGroups { get; } = [];
    public ObservableCollection<object> DashboardItems { get; } = [];
    public bool HasGraphData => AttemptsGraphPoints.Count > 0;

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
            case nameof(AppMetaData.ShowBackgroundSwirl):
                OnPropertyChanged(nameof(ShowBackgroundSwirl));
                break;
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

    /// <summary>
    /// Rebuilds the visible list: everything filed in the open folder, ranked by the
    /// shared search, ordered by the chosen sort. While a search is running the scope
    /// widens to the open folder's subfolders, but never above where you are standing.
    /// </summary>
    public void RefreshLibraryView()
    {
        if (_suspendRefresh) return;

        // The open folder can disappear underneath us (deleted here, or replaced by a
        // restored backup); fall back to the main menu rather than showing an empty void.
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

        var ordered = visible.SearchAndSort(SearchText, SelectedSortOption?.Mode ?? SortMode.Relevance, TypePriority);

        DashboardItems.Clear();
        foreach (var item in ordered)
        {
            if (item is FlashCardDeck deck) deck.IsSelectedForMultiReview = _selectedDeckIds.Contains(deck.ID);
            DashboardItems.Add(item);
        }

        RefreshEmptyState();
    }

    private static int TypePriority(ISearchable item) => item switch
    {
        Folder => FolderPriority,
        StudyGroup => GroupPriority,
        _ => SetPriority,
    };

    /// <summary>
    /// Recomputes folder card counts, the "found in" labels and the breadcrumb trail.
    /// Only reloading or navigating changes these, so searching does not pay for them.
    /// </summary>
    private void RefreshFolderMetadata()
    {
        _folderTree.ApplyCounts(Decks, StudyGroups);
        _folderTree.ApplyPaths(Decks, StudyGroups, CurrentFolderID);

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
            IsInFolder ? $"{CurrentFolderName} is empty. Create a set here, or move one in." :
            "No flashcard sets yet. Create one to get started.";

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

    /// <summary> Deletes the folder only: its contents move up to the folder's parent. </summary>
    public void DeleteFolder(Folder folder)
    {
        FolderRepository.DeleteFolder(folder.ID);

        // Standing inside the folder that just went away, step up to where its contents went.
        if (CurrentFolderID == folder.ID) CurrentFolderID = folder.ParentFolderID;

        ReloadLibrary();
    }

    /// <summary> Refiles a set, group or folder. Returns false when the move is not allowed. </summary>
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

    /// <summary> Reloads folders, sets and groups from the database, then rebuilds the view. </summary>
    public void ReloadLibrary()
    {
        _folderTree = FolderTree.Load();

        var savedDecks = FlashCardRepository.GetAllDecks();
        Decks.Clear();
        foreach (var deck in savedDecks) Decks.Add(deck);

        var savedGroups = FlashCardRepository.GetAllStudyGroups();
        StudyGroups.Clear();
        foreach (var group in savedGroups) StudyGroups.Add(group);

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
    }

    public void DeleteStudyGroup(StudyGroup groupToDelete)
    {
        FlashCardRepository.DeleteStudyGroup(groupToDelete.ID);
        ReloadLibrary();
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

    /// <summary> Creates a set filed in the folder currently open. </summary>
    public FlashCardDeck CreateNewDeck()
    {
        var newDeck = new FlashCardDeck("New Flashcard Set");
        FlashCardRepository.SaveNewDeck(newDeck);

        if (CurrentFolderID is ulong folderID) FolderRepository.MoveDeck(newDeck.ID, folderID);

        ReloadLibrary();
        return newDeck;
    }
}
