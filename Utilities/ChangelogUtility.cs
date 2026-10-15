using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReviFlash.Utilities;

/// <summary> One version's notes, written in card formatting. </summary>
public sealed record ReleaseNotes(string Version, string Body);

/// <summary> The changelog shipped in Assets/Changelog.txt: each version starts with a "=== 1.4.0" line, newest first. </summary>
public static class ChangelogUtility
{
    private const string VersionMarker = "=== ";
    private const string ResourceName = "ReviFlash.Changelog.txt";

    private static IReadOnlyList<ReleaseNotes>? _bundled;

    public static IReadOnlyList<ReleaseNotes> Bundled => _bundled ??= Parse(ReadBundled());

    public static List<ReleaseNotes> Parse(string text)
    {
        var notes = new List<ReleaseNotes>();
        string? version = null;
        var body = new List<string>();

        void Flush()
        {
            if (version is not null) notes.Add(new ReleaseNotes(version, string.Join('\n', body).Trim()));
            body.Clear();
        }

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith(VersionMarker, StringComparison.Ordinal))
            {
                Flush();
                version = line[VersionMarker.Length..].Trim();
            }
            else if (version is not null)
            {
                body.Add(line);
            }
        }

        Flush();
        return notes;
    }

    public static bool HasNotesFor(string version) => Bundled.Any(notes => notes.Version == version);

    /// <summary> The changelog opens once after an update: never on a fresh install, nor for a version with no notes. </summary>
    public static bool ShouldShowAfterUpdate(string previousVersion, string currentVersion, bool enabled, bool hasNotes) =>
        enabled && hasNotes && previousVersion != currentVersion;

    private static string ReadBundled()
    {
        using var stream = typeof(ChangelogUtility).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
