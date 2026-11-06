using System;

namespace ReviFlash.Utilities;

/// <summary> Updating to 1.5 switches to its new default theme once; any theme picked after that stays. </summary>
public static class ThemeUpgrade
{
    public static readonly Version NewLook = new(1, 5, 0);

    /// <param name="previousVersion"> The version text saved by the last launch, empty on a fresh install. </param>
    public static string ThemeAfterUpdate(string theme, string previousVersion, string currentVersion, string newDefault) =>
        Parse(previousVersion) is { } before && before < NewLook && Parse(currentVersion) is { } now && now >= NewLook
            ? newDefault
            : theme;

    private static Version? Parse(string versionText) =>
        Version.TryParse(versionText.Replace("Version", "", StringComparison.Ordinal).Trim().Split('-')[0], out var version) ? version : null;
}
