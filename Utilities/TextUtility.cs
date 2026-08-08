using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ReviFlash.ViewModels;

namespace ReviFlash.Utilities;

/// <summary> Precompiled regex & other text utilis  </summary>
public static partial class TextUtility
{
    // --- Regex ---

    [GeneratedRegex(@"\$\$(.+?)\$\$|\$(.+?)\$", RegexOptions.Singleline)]
    public static partial Regex InlineMathRegex();

    [GeneratedRegex(@"(\d+)\.(\d+)\.(\d+)")]
    public static partial Regex VersionRegex();

    // --- Json ---

    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    public static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    // --- Paths ---

    public const string MetadataFileName = "metadata.json";
    public const string DatabaseFileName = "reviflash.db";

    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "ReviFlash"
    );

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

    // --- Versions ---

    public static string VersionText => $"Version P-{GetAssemblyVersionText()}";

    private static string GetAssemblyVersionText()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
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
}