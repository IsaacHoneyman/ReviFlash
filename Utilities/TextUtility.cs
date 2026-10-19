using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ReviFlash.ViewModels;

namespace ReviFlash.Utilities;

public static partial class TextUtility
{
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    public static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public const string MetadataFileName = "metadata.json";
    public const string DatabaseFileName = "reviflash.db";

    /// <summary> REVIFLASH_DATA_DIR overrides the folder, so tests get their own on every OS (Windows and macOS ignore XDG_DATA_HOME). </summary>
    private static readonly string AppDataDirectory = Environment.GetEnvironmentVariable("REVIFLASH_DATA_DIR") is { Length: > 0 } overridden
        ? overridden
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReviFlash");

    static TextUtility()
    {
        if (!Directory.Exists(AppDataDirectory))
        {
            Directory.CreateDirectory(AppDataDirectory);
        }
    }

    public static string BaseDirectory => AppDataDirectory;
    public static string MetadataPath => Path.Combine(AppDataDirectory, MetadataFileName);
    public static string DatabasePath => Path.Combine(AppDataDirectory, DatabaseFileName);

    public static string VersionText => $"Version {VersionNumber}";

    public static string VersionNumber => GetAssemblyVersionText();

    private static string GetAssemblyVersionText()
    {
        var assembly = typeof(DashboardViewModel).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        var version = assembly.GetName().Version;
        return version is null
            ? "Unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary> "m:ss", or "h:mm:ss" from an hour (or always, for a running clock); hours keep counting past a day. </summary>
    public static string FormatTime(TimeSpan time, bool alwaysShowHours = false)
    {
        var hours = (int)time.TotalHours;
        return hours > 0 || alwaysShowHours ? $"{hours}:{time:mm\\:ss}" : time.ToString(@"m\:ss");
    }

    public static string FormatTime(int seconds, bool alwaysShowHours = false) =>
        FormatTime(TimeSpan.FromSeconds(seconds), alwaysShowHours);

    /// <summary> "1 deck", "2 decks"; pass <paramref name="plural"/> for irregular nouns. </summary>
    public static string Plural(int count, string noun, string? plural = null) =>
        count == 1 ? $"1 {noun}" : $"{count} {plural ?? noun + "s"}";

    public const string Ellipsis = "…";

    /// <summary> <paramref name="text"/> if it <paramref name="fits"/>, otherwise its longest start that fits with an ellipsis on the end. </summary>
    public static string TrimToFit(string text, Func<string, bool> fits)
    {
        if (fits(text)) return text;

        int low = 0, high = text.Length - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (fits(WithEllipsis(text, mid))) low = mid;
            else high = mid - 1;
        }

        return WithEllipsis(text, low);
    }

    private static string WithEllipsis(string text, int length) => text[..length].TrimEnd() + Ellipsis;

    /// <summary> A local time as "just now", "5 min ago", "today at 14:05", "yesterday", "3 Oct" or "3 Oct 2025". </summary>
    /// <summary> A count short enough for a badge: 950, 1.2k, 12k. </summary>
    public static string Compact(int count) => count switch
    {
        < 1000 => count.ToString(),
        < 10_000 => (Math.Floor(count / 100.0) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k",
        _ => (count / 1000) + "k",
    };

    public static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary> A study day relative to today: "today", "yesterday", "3 days ago", then "d MMM". </summary>
    public static string FormatDay(DateTime day)
    {
        var days = (DateTime.Today - day.Date).Days;
        if (days <= 0) return "today";
        if (days == 1) return "yesterday";
        if (days < 7) return $"{days} days ago";
        return day.Year == DateTime.Today.Year ? day.ToString("d MMM") : day.ToString("d MMM yyyy");
    }

    public static string FormatWhen(DateTime local)
    {
        var now = DateTime.Now;
        var ago = now - local;

        if (ago < TimeSpan.FromMinutes(1)) return "just now";
        if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes} min ago";
        if (local.Date == now.Date) return $"today at {local:HH:mm}";
        if (local.Date == now.Date.AddDays(-1)) return "yesterday";
        return local.Year == now.Year ? local.ToString("d MMM") : local.ToString("d MMM yyyy");
    }
}