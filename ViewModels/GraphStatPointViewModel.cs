using System;

namespace ReviFlash.ViewModels;

public class GraphStatPointViewModel : ViewModelBase
{
    private const int MaxChartHeight = 150;

    public string Label { get; }
    public int CorrectCount { get; }
    public int TotalCount { get; }
    public int IncorrectCount => Math.Max(0, TotalCount - CorrectCount);
    public int TimeTakenSeconds { get; }

    public int AttemptsBarHeight { get; }
    public int CorrectBarHeight { get; }
    public int IncorrectBarHeight => Math.Max(0, AttemptsBarHeight - CorrectBarHeight);
    public int TimeBarHeight { get; }

    public bool HasAttempts => TotalCount > 0;
    public bool HasTime => TimeTakenSeconds > 0;

    public string CorrectCountText => CorrectCount.ToString();
    public string IncorrectCountText => IncorrectCount.ToString();

    public string AccuracyText => TotalCount > 0
        ? $"{Math.Round((double)CorrectCount / TotalCount * 100, 1)}%"
        : "0%";

    public string TimeText => TimeSpan.FromSeconds(TimeTakenSeconds).ToString(TimeTakenSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    public string TimeValueText => TimeText;

    public string AttemptsTooltip => $"{Label}: {CorrectCount}/{TotalCount} correct, {TimeText}";
    public string TimeTooltip => $"{Label}: {TimeText} spent";

    public GraphStatPointViewModel(string label, int correctCount, int totalCount, int timeTakenSeconds, int maxAttempts, int maxTimeSeconds)
    {
        Label = label;
        CorrectCount = correctCount;
        TotalCount = totalCount;
        TimeTakenSeconds = timeTakenSeconds;

        AttemptsBarHeight = totalCount > 0 && maxAttempts > 0
            ? Math.Max(8, (int)Math.Round((double)totalCount * MaxChartHeight / maxAttempts))
            : 0;

        CorrectBarHeight = totalCount > 0
            ? (int)Math.Round((double)AttemptsBarHeight * correctCount / totalCount)
            : 0;

        TimeBarHeight = timeTakenSeconds > 0 && maxTimeSeconds > 0
            ? Math.Max(8, (int)Math.Round((double)timeTakenSeconds * MaxChartHeight / maxTimeSeconds))
            : 0;
    }
}