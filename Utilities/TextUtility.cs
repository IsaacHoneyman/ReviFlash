using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReviFlash.Utilities;

/// <summary> Precompiled Regex & Json Utility </summary>
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
}