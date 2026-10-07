using System;

namespace ReviFlash.ViewModels;

public class GraphStatPointViewModel : ViewModelBase
{
    private const int MaxChartHeight = 150;

    public string Label { get; }
    public int CorrectCount { get; }
    public int TotalCount { get; }
    public int IncorrectCount => Math.Max(0, TotalCount - CorrectCount);

    /// <summary> Flashcard time. </summary>
    public int TimeTakenSeconds { get; }

    /// <summary> Time in notes, stacked under the flashcard time in the study time chart. </summary>
    public int NoteSeconds { get; }

    public int TotalSeconds => TimeTakenSeconds + NoteSeconds;

    public int AttemptsBarHeight { get; }
    public int CorrectBarHeight { get; }
    public int IncorrectBarHeight => Math.Max(0, AttemptsBarHeight - CorrectBarHeight);
    public int TimeBarHeight { get; }
    public int NoteBarHeight { get; }

    public bool HasAttempts => TotalCount > 0;
    public bool HasTime => TotalSeconds > 0;

    public string CorrectCountText => CorrectCount.ToString();
    public string IncorrectCountText => IncorrectCount.ToString();
    public string AccuracyText => TotalCount > 0 ? $"{Math.Round((double)CorrectCount / TotalCount * 100, 1)}%" : "0%";
    public string TimeText => FormatSeconds(TotalSeconds);
    public string TimeValueText => TimeText;
    public string AttemptsTooltip => $"{Label}: {CorrectCount}/{TotalCount} correct, {FormatSeconds(TimeTakenSeconds)}";
    public string TimeTooltip => NoteSeconds == 0
        ? $"{Label}: {TimeText} spent"
        : $"{Label}: {TimeText} spent ({FormatSeconds(TimeTakenSeconds)} flashcards, {FormatSeconds(NoteSeconds)} notes)";

    public GraphStatPointViewModel(string label, int correctCount, int totalCount, int timeTakenSeconds, int maxAttempts, int maxTimeSeconds,
        int noteSeconds = 0)
    {
        Label = label;
        CorrectCount = correctCount;
        TotalCount = totalCount;
        TimeTakenSeconds = timeTakenSeconds;
        NoteSeconds = noteSeconds;

        AttemptsBarHeight = totalCount > 0 && maxAttempts > 0
            ? Math.Max(8, (int)Math.Round((double)totalCount * MaxChartHeight / maxAttempts)) : 0;

        CorrectBarHeight = totalCount > 0
            ? (int)Math.Round((double)AttemptsBarHeight * correctCount / totalCount) : 0;

        TimeBarHeight = TotalSeconds > 0 && maxTimeSeconds > 0
            ? Math.Max(8, (int)Math.Round((double)TotalSeconds * MaxChartHeight / maxTimeSeconds)) : 0;

        NoteBarHeight = TotalSeconds > 0
            ? (int)Math.Round((double)TimeBarHeight * noteSeconds / TotalSeconds) : 0;
    }

    private static string FormatSeconds(int seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}
