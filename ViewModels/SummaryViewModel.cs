using System;

namespace ReviFlash.ViewModels;

public class SummaryViewModel : ViewModelBase
{
    public int Score { get; }
    public int Total { get; }
    public TimeSpan TimeTaken { get; }
    
    public double Percentage => Total > 0 ? Math.Round((double)Score / Total * 100, 1) : 0;
    public string TimeFormatted => TimeTaken.ToString(@"mm\:ss");

    public string Grade => Percentage switch
    {
        >= 90 => "A*",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        >= 50 => "D",
        _ => "U"
    };

    public SummaryViewModel(int score, int total, TimeSpan time)
    {
        Score = score;
        Total = total;
        TimeTaken = time;
    }
}