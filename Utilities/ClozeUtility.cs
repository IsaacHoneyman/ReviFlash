using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReviFlash.Utilities;

/// <summary> Cloze blanks in raw card text (so inside $...$ too): \C{...} is its own question, \C2{...} blanks sharing a number hide together. </summary>
public static class ClozeUtility
{
    public const string HiddenMarker = "[...]";

    // Start is the backslash, End is just past the closing brace, Group is the number or a unique key when unnumbered.
    private sealed record Blank(int Start, int ContentStart, int ContentEnd, string Group)
    {
        public int End => ContentEnd + 1;
    }

    /// <summary> Each group of blanks that becomes a question, in the order they first appear. </summary>
    public static IReadOnlyList<string> Groups(string text) => FindBlanks(text).Where(blank => HasContent(text, blank)).Select(blank => blank.Group).Distinct().ToList();

    public static bool HasBlanks(string text) => FindBlanks(text).Any(blank => HasContent(text, blank));

    /// <summary> True when a blank hides an image, which can't be answered by typing. </summary>
    public static bool AnyBlankHasImage(string text) =>
        FindBlanks(text).Any(blank => ImageToken.Shows(text[blank.ContentStart..blank.ContentEnd]));

    /// <summary> The question: the group's blanks shown as [...], every other blank as plain text. </summary>
    public static string Mask(string text, string group) => Render(text, group, reveal: false);

    /// <summary> The answer side: the group's blanks in bold, every other blank as plain text. </summary>
    public static string Reveal(string text, string group) => Render(text, group, reveal: true);

    /// <summary> What has to be typed for the group: its blanks' text, comma separated when there are several. </summary>
    public static string Answer(string text, string group) => string.Join(", ", Answers(text, group));

    /// <summary> The plain text of each of the group's blanks, in order. </summary>
    public static IReadOnlyList<string> Answers(string text, string group)
    {
        var answers = new List<string>();
        var coveredUntil = 0;

        foreach (var blank in FindBlanks(text))
        {
            if (blank.Start < coveredUntil || blank.Group != group || !HasContent(text, blank)) continue;
            coveredUntil = blank.End;
            answers.Add(PlainText(text[blank.ContentStart..blank.ContentEnd]).Trim());
        }

        return answers;
    }

    private static bool HasContent(string text, Blank blank) => !text.AsSpan(blank.ContentStart, blank.ContentEnd - blank.ContentStart).IsWhiteSpace();

    private static string Render(string text, string group, bool reveal)
    {
        var blanks = FindBlanks(text);
        var output = new StringBuilder(text.Length);
        // The blanks being read through, innermost last: where their text ends and what closes them.
        var open = new Stack<(int ContentEnd, string Closing, bool Target)>();
        var targets = 0;
        var position = 0;
        var index = 0;

        while (index < blanks.Count || open.Count > 0)
        {
            if (open.Count > 0 && (index >= blanks.Count || open.Peek().ContentEnd < blanks[index].Start))
            {
                var (contentEnd, closing, target) = open.Pop();
                output.Append(text, position, contentEnd - position).Append(closing);
                position = contentEnd + 1;
                if (target) targets--;
                continue;
            }

            var blank = blanks[index++];
            output.Append(text, position, blank.Start - position);
            position = blank.ContentStart;

            if (blank.Group != group || targets > 0 || !HasContent(text, blank))
            {
                open.Push((blank.ContentEnd, "", false));
            }
            else if (reveal)
            {
                output.Append(@"\B{");
                open.Push((blank.ContentEnd, "}", true));
                targets++;
            }
            else
            {
                output.Append(HiddenMarker);
                position = blank.End;
                while (index < blanks.Count && blanks[index].Start < blank.End) index++;
            }
        }

        return output.Append(text, position, text.Length - position).ToString();
    }

    private static bool IsEscapable(char c) => c is '\\' or '$' or '-' or '{' or '}';

    /// <summary> Blank text as it is typed: escapes undone and \B{ \I{ \U{ \H1{ and nested \C{ formatting removed; maths is kept as written. </summary>
    private static string PlainText(string content)
    {
        var output = new StringBuilder(content.Length);
        // One entry per open brace: true for a command's, false for a literal one.
        var braces = new Stack<bool>();
        var i = 0;

        while (i < content.Length)
        {
            var c = content[i];

            if (c == '$')
            {
                var delimiter = i + 1 < content.Length && content[i + 1] == '$' ? "$$" : "$";
                var close = FindClosingDollar(content, i + delimiter.Length, delimiter);
                if (close > i + delimiter.Length)
                {
                    output.Append(content, i, close + delimiter.Length - i);
                    i = close + delimiter.Length;
                    continue;
                }
            }
            else if (c == '\\')
            {
                if (i + 1 < content.Length && IsEscapable(content[i + 1]))
                {
                    output.Append(content[i + 1]);
                    i += 2;
                    continue;
                }

                var commandEnd = TryReadOpening(content, i, out _, out var blankStart) ? blankStart : FormatCommandEnd(content, i);
                if (commandEnd > 0)
                {
                    braces.Push(true);
                    i = commandEnd;
                    continue;
                }
            }
            else if (c == '{')
            {
                braces.Push(false);
            }
            else if (c == '}' && braces.Count > 0 && braces.Pop())
            {
                i++;
                continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    /// <summary> The index just past \B{, \I{, \U{ or \H1{ to \H3{ at <paramref name="index"/>, or -1. </summary>
    private static int FormatCommandEnd(string text, int index)
    {
        if (index + 2 < text.Length && text[index + 1] is 'B' or 'I' or 'U' && text[index + 2] == '{') return index + 3;
        if (index + 3 < text.Length && text[index + 1] == 'H' && text[index + 2] is '1' or '2' or '3' && text[index + 3] == '{') return index + 4;
        return -1;
    }

    private static int FindClosingDollar(string text, int start, string delimiter)
    {
        var index = start;
        while (index < text.Length)
        {
            var found = text.IndexOf(delimiter, index, StringComparison.Ordinal);
            if (found < 0) return -1;
            if (!LatexUtility.IsEscaped(text, found, start)) return found;
            index = found + 1;
        }
        return -1;
    }

    /// <summary> Every closed blank, nested ones too, in the order they start. </summary>
    private static List<Blank> FindBlanks(string text)
    {
        var found = new List<(int Start, string? Number, int ContentStart, int ContentEnd)>();
        // One entry per open brace; Start is -1 for a plain brace.
        var open = new Stack<(int Start, string? Number, int ContentStart)>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\\')
            {
                if (TryReadOpening(text, i, out var number, out var contentStart))
                {
                    open.Push((i, number, contentStart));
                    i = contentStart;
                    continue;
                }

                // Skip whatever a backslash escapes, so \\C or \{ is never read as the start of a blank.
                i += i + 1 < text.Length && IsEscapable(text[i + 1]) ? 2 : 1;
                continue;
            }

            if (c == '{')
            {
                open.Push((-1, null, i + 1));
            }
            else if (c == '}' && open.Count > 0)
            {
                var (start, number, contentStart) = open.Pop();
                if (start >= 0) found.Add((start, number, contentStart, i));
            }
            i++;
        }

        found.Sort((a, b) => a.Start.CompareTo(b.Start));
        var unnumbered = 0;
        return found.Select(blank => new Blank(blank.Start, blank.ContentStart, blank.ContentEnd, blank.Number ?? $"#{unnumbered++}")).ToList();
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
}
