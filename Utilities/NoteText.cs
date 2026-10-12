using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReviFlash.Utilities;

/// <summary> A heading in a note: its level (1 to 3), what was typed inside \Hn{...}, and where it starts. </summary>
public sealed record NoteHeading(int Level, string Source, int Index);

/// <summary> Note content as blocks: paragraphs split at blank lines, never inside a $$...$$ block. </summary>
public static partial class NoteText
{
    [GeneratedRegex(@"\\H([1-3])\{")]
    private static partial Regex HeadingOpeningRegex();

    [GeneratedRegex(@"\\[A-Za-z]+\d*\{|[{}$\\]")]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(@"^[ \t]*- ", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\n[^\S\n]*\n")]
    private static partial Regex BlankLineRegex();

    /// <summary> Splits content into blocks at blank lines, outside display maths. Never returns an empty list. </summary>
    public static List<string> SplitBlocks(string? content)
    {
        var blocks = new List<string>();
        var current = new List<string>();
        var text = (content ?? "").Replace("\r", "");
        var spans = DisplayMathSpans(text, out _);
        var offset = 0;

        void Flush()
        {
            if (current.Count > 0) blocks.Add(string.Join('\n', current));
            current.Clear();
        }

        foreach (var line in text.Split('\n'))
        {
            var lineStart = offset;
            offset += line.Length + 1;

            if (string.IsNullOrWhiteSpace(line) && !spans.Any(span => span.Start < lineStart && lineStart < span.End))
            {
                Flush();
                continue;
            }

            current.Add(line);
        }

        Flush();
        if (blocks.Count == 0) blocks.Add("");
        return blocks;
    }

    /// <summary> The content the blocks make, one blank line between each. Empty blocks are dropped. </summary>
    public static string JoinBlocks(IEnumerable<string?> blocks) =>
        string.Join("\n\n", blocks.Select(block => (block ?? "").Trim('\n', '\r')).Where(block => !string.IsNullOrWhiteSpace(block)));

    /// <summary> Every \H1{...} to \H3{...} in the text, in order, with what was typed inside it. </summary>
    public static List<NoteHeading> Headings(string text)
    {
        var headings = new List<NoteHeading>();
        foreach (Match match in HeadingOpeningRegex().Matches(text))
        {
            var contentStart = match.Index + match.Length;
            var end = FindClosingBrace(text, contentStart);
            var source = end < 0 ? text[contentStart..] : text[contentStart..end];
            if (!string.IsNullOrWhiteSpace(source)) headings.Add(new NoteHeading(match.Groups[1].Value[0] - '0', source.Trim(), match.Index));
        }
        return headings;
    }

    /// <summary> "\H2{Definition}\nA scalar..." gives ("Definition", "A scalar..."); null heading if the paragraph doesn't open with one. </summary>
    public static (string? Heading, string Body) SplitLeadingHeading(string text)
    {
        var trimmed = text.TrimStart();
        var match = HeadingOpeningRegex().Match(trimmed);
        if (!match.Success || match.Index != 0) return (null, text);

        var end = FindClosingBrace(trimmed, match.Length);
        if (end < 0) return (null, text);

        var heading = trimmed[match.Length..end].Trim();
        return heading.Length == 0 ? (null, text) : (heading, trimmed[(end + 1)..].Trim());
    }

    public static string? LastHeading(string text) => Headings(text).LastOrDefault()?.Source;

    /// <summary> Words in the note, not counting formatting commands or maths delimiters. </summary>
    public static int WordCount(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        var plain = MarkupRegex().Replace(BulletRegex().Replace(content, ""), " ");
        return plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary> True when <paramref name="text"/> ends inside a $$...$$ block that hasn't been closed yet. </summary>
    public static bool EndsInDisplayMath(string text)
    {
        DisplayMathSpans(text.Replace("\r", ""), out var unclosed);
        return unclosed;
    }

    /// <summary> The $$...$$ blocks in the text, read the way LatexUtility reads $, $$ and \$; a $$ with no closing $$ is plain text. </summary>
    private static List<(int Start, int End)> DisplayMathSpans(string text, out bool unclosedDisplay)
    {
        var spans = new List<(int Start, int End)>();
        unclosedDisplay = false;
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }
            if (text[i] != '$')
            {
                i++;
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '$')
            {
                var close = FindClosingDollar(text, i + 2, "$$", text.Length);
                if (close >= 0)
                {
                    spans.Add((i, close + 2));
                    i = close + 2;
                    continue;
                }
                unclosedDisplay = true;
                i++;
                continue;
            }

            // Inline maths can't reach past a blank line, because each paragraph is read on its own.
            var blank = BlankLineRegex().Match(text, i);
            var close1 = FindClosingDollar(text, i + 1, "$", blank.Success ? blank.Index : text.Length);
            i = close1 >= 0 ? close1 + 1 : i + 1;
        }

        return spans;
    }

    private static int FindClosingDollar(string text, int start, string delimiter, int limit)
    {
        var index = start;
        while (index < limit)
        {
            var found = text.IndexOf(delimiter, index, limit - index, StringComparison.Ordinal);
            if (found < 0) return -1;
            if (!LatexUtility.IsEscaped(text, found, start)) return found;
            index = found + 1;
        }
        return -1;
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
