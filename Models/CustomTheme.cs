using System;
using System.Collections.Generic;
using System.Linq;

namespace ReviFlash.Models;

/// <summary> A theme the user made and named: three picked colours, with any single theme colour overridable. Colours are "#RRGGBB". </summary>
public sealed class CustomTheme
{
    public const int MaxNameLength = 30;

    public string Name { get; set; } = "My Theme";
    public string Background { get; set; } = "#1A1528";
    public string Accent { get; set; } = "#FF71CE";
    public string SecondAccent { get; set; } = "#85EB8F";

    /// <summary> Theme colour name to "#RRGGBB", replacing the derived colour. </summary>
    public Dictionary<string, string> Overrides { get; set; } = [];

    public CustomTheme Copy() => new()
    {
        Name = Name,
        Background = Background,
        Accent = Accent,
        SecondAccent = SecondAccent,
        Overrides = new Dictionary<string, string>(Overrides),
    };

    /// <summary> What's wrong with <paramref name="name"/> as a theme name, given the names already taken; null when it's fine. </summary>
    public static string? NameProblem(string? name, IEnumerable<string> taken)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0) return "Give your theme a name.";
        if (trimmed.Length > MaxNameLength) return $"Theme names can be up to {MaxNameLength} characters.";
        return taken.Contains(trimmed, StringComparer.OrdinalIgnoreCase) ? $"There's already a theme called {trimmed}." : null;
    }
}
