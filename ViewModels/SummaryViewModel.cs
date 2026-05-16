using System;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

public class SummaryViewModel : ViewModelBase
{
    public int Score { get; }
    public int Total { get; }
    public TimeSpan TimeTaken { get; }
    public bool IsPartialSession { get; }
    
    public double Percentage => Total > 0 ? Math.Round((double)Score / Total * 100, 1) : 0;
    public string TimeFormatted => TimeTaken.TotalHours >= 1
        ? TimeTaken.ToString(@"hh\:mm\:ss")
        : TimeTaken.ToString(@"mm\:ss");
    public string SessionMarker => IsPartialSession ? "(Partial)" : "";

    public string Grade => GradeCalculator.CalculateGradeWithDefault(Score, Total);

    public SummaryViewModel(int score, int total, TimeSpan time, bool isPartialSession = false)
    {
        Score = score;
        Total = total;
        TimeTaken = time;
        IsPartialSession = isPartialSession;
    }
}