using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReviFlash.Utilities;

/// <summary>
/// Cloze blanks: \C{...} hides its text in a question of its own, and \C2{...} blanks sharing a
/// number are hidden together. Works on the raw card text, so blanks can sit inside $...$ maths too.
/// </summary>
public static class ClozeUtility
{
    public const string HiddenMarker = "[...]";

    /// <param name="Start"> Index of the backslash. </param>
    /// <param name="End"> Index just past the closing brace. </param>
    /// <param name="Group"> The blank's number, or a unique key for an unnumbered blank. </param>
    private sealed record Blank(int Start, int End, string Group, string Content);

    /// <summary> Each group of blanks that becomes a question, in the order they first appear. </summary>
    public static IReadOnlyList<string> Groups(string text) => FindBlanks(text).Select(blank => blank.Group).Distinct().ToList();

    public static bool HasBlanks(string text) => FindBlanks(text).Count > 0;

    /// <summary> The question: the group's blanks shown as [...], every other blank as plain text. </summary>
    public static string Mask(string text, string group) =>
        Replace(text, blank => blank.Group == group ? HiddenMarker : blank.Content);

    /// <summary> The answer side: the group's blanks in bold, every other blank as plain text. </summary>
    public static string Reveal(string text, string group) =>
        Replace(text, blank => blank.Group == group ? $@"\B{{{blank.Content}}}" : blank.Content);

    /// <summary> What has to be typed for the group: its blanks' text, comma separated when there are several. </summary>
    public static string Answer(string text, string group) =>
        string.Join(", ", FindBlanks(text).Where(blank => blank.Group == group).Select(blank => blank.Content.Trim()));

    private static string Replace(string text, System.Func<Blank, string> replacement)
    {
        var output = new StringBuilder(text.Length);
        var position = 0;

        foreach (var blank in FindBlanks(text))
        {
            output.Append(text, position, blank.Start - position).Append(replacement(blank));
            position = blank.End;
        }

        return output.Append(text, position, text.Length - position).ToString();
    }

    private static List<Blank> FindBlanks(string text)
    {
        var blanks = new List<Blank>();
        var unnumbered = 0;
        var i = 0;

        while (i < text.Length)
        {
            if (TryReadOpening(text, i, out var number, out var contentStart))
            {
                var close = FindClosingBrace(text, contentStart);
                if (close >= 0)
                {
                    var group = number ?? $"#{unnumbered++}";
                    blanks.Add(new Blank(i, close + 1, group, text[contentStart..close]));
                    i = close + 1;
                    continue;
                }
            }

            // Skip whatever a backslash escapes, so \\C or \$ is never read as the start of a blank.
            i += text[i] == '\\' ? 2 : 1;
        }

        return blanks;
    }

    /// <summary> \C{ or \C&lt;digits&gt;{ at <paramref name="index"/>; <paramref name="number"/> is null when unnumbered. </summary>
    public static bool TryReadOpening(string text, int index, out string? number, out int contentStart)
    {
        number = null;
        contentStart = -1;
        if (index + 2 >= text.Length || text[index] != '\\' || text[index + 1] != 'C') return false;

        var digitsEnd = index + 2;
        while (digitsEnd < text.Length && char.IsAsciiDigit(text[digitsEnd])) digitsEnd++;
        if (digitsEnd >= text.Length || text[digitsEnd] != '{') return false;

        if (digitsEnd > index + 2) number = text[(index + 2)..digitsEnd].TrimStart('0') is { Length: > 0 } n ? n : "0";
        contentStart = digitsEnd + 1;
        return true;
    }

    private static int FindClosingBrace(string text, int start)
    {
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }
}
