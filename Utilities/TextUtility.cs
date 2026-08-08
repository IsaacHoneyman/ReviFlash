using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;
    public static string MetadataPath => Path.Combine(BaseDirectory, MetadataFileName);
    public static string DatabasePath => Path.Combine(BaseDirectory, DatabaseFileName);
}