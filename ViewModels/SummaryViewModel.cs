using System;
using ReviFlash.Utilities;

using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.ViewModels;

public class SummaryViewModel(int score, int total, TimeSpan time, bool isPartialSession = false) : ViewModelBase
{
    public int Score { get; } = score;
    public int Total { get; } = total;
    public TimeSpan TimeTaken { get; } = time;
    public bool IsPartialSession { get; } = isPartialSession;
    public Action? OnReturnToDashboard { get; set; }

    public double Percentage => AccuracyPercent(Score, Total);
    public string TimeFormatted => TextUtility.FormatTime(TimeTaken);
    public string SessionMarker => IsPartialSession ? "(Partial)" : "";
    public string Grade => CalculateGradeWithDefault(Score, Total);
}