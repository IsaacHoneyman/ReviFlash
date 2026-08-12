using System;

using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.ViewModels;

public class SummaryViewModel(int score, int total, TimeSpan time, bool isPartialSession = false) : ViewModelBase
{
    public int Score { get; } = score;
    public int Total { get; } = total;
    public TimeSpan TimeTaken { get; } = time;
    public bool IsPartialSession { get; } = isPartialSession;
    public Action? OnReturnToDashboard { get; set; }

    public double Percentage => Total > 0 ? Math.Round((double)Score / Total * 100, 1) : 0;
    public string TimeFormatted => TimeTaken.TotalHours >= 1
        ? TimeTaken.ToString(@"hh\:mm\:ss")
        : TimeTaken.ToString(@"mm\:ss");
    public string SessionMarker => IsPartialSession ? "(Partial)" : "";
    public string Grade => CalculateGradeWithDefault(Score, Total);
}