using System;
using System.Collections.Generic;
using ReviFlash.Utilities;

namespace ReviFlash.ViewModels;

/// <summary> A day in the study calendar, shaded by how long was spent studying. </summary>
public sealed class HeatmapDay(DateOnly date, int seconds, bool isFuture)
{
    public DateOnly Date { get; } = date;
    public int Seconds { get; } = seconds;
    public bool IsFuture { get; } = isFuture;

    public int Level => Seconds switch
    {
        <= 0 => 0,
        < 15 * 60 => 1,
        < 30 * 60 => 2,
        < 60 * 60 => 3,
        _ => 4,
    };

    public bool IsLevel1 => Level == 1;
    public bool IsLevel2 => Level == 2;
    public bool IsLevel3 => Level == 3;
    public bool IsLevel4 => Level == 4;

    public string Tooltip => Seconds > 0
        ? $"{Date:ddd d MMM yyyy}: {TextUtility.FormatTime(TimeSpan.FromSeconds(Seconds))}"
        : $"{Date:ddd d MMM yyyy}: nothing studied";
}

/// <summary> A column of the study calendar, Monday to Sunday, with a month name over the first week of each month. </summary>
public sealed class HeatmapWeek(string monthLabel, IReadOnlyList<HeatmapDay> days)
{
    public string MonthLabel { get; } = monthLabel;
    public IReadOnlyList<HeatmapDay> Days { get; } = days;
}

/// <summary> A set or note in "Where your time went", with a bar sized against the one with the most time. </summary>
public sealed class TimeSpentItem(string name, string kind, int seconds, int mostSeconds)
{
    private const double MaxBarWidth = 360;

    public string Name { get; } = name;
    public string Kind { get; } = kind;
    public int Seconds { get; } = seconds;

    public string TimeText => TextUtility.FormatTime(TimeSpan.FromSeconds(Seconds));
    public double BarWidth => mostSeconds > 0 ? Math.Max(4, MaxBarWidth * Seconds / mostSeconds) : 0;
}
