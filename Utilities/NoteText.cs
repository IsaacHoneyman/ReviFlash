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

    /// <summary> Splits content into blocks at blank lines, outside display maths. Never returns an empty list. </summary>
    public static List<string> SplitBlocks(string? content)
    {
        var blocks = new List<string>();
        var current = new List<string>();
        var inDisplayMath = false;

        void Flush()
        {
            if (current.Count > 0) blocks.Add(string.Join('\n', current));
            current.Clear();
        }

        foreach (var line in (content ?? "").Replace("\r", "").Split('\n'))
        {
            if (!inDisplayMath && string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            current.Add(line);
            if (CountDisplayDelimiters(line) % 2 == 1) inDisplayMath = !inDisplayMath;
        }

        Flush();
        if (blocks.Count == 0) blocks.Add("");
        return blocks;
    }

    /// <summary> The content the blocks make, one blank line between each. Empty blocks are dropped. </summary>
    public static string JoinBlocks(IEnumerable<string> blocks) =>
        string.Join("\n\n", blocks.Select(block => block.Trim('\n', '\r')).Where(block => !string.IsNullOrWhiteSpace(block)));

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

    /// <summary> True when <paramref name="text"/> ends inside an unclosed $$...$$ block. </summary>
    public static bool EndsInDisplayMath(string text) =>
        text.Replace("\r", "").Split('\n').Sum(CountDisplayDelimiters) % 2 == 1;

    /// <summary> $$ delimiters on a line, skipping escaped \$. </summary>
    private static int CountDisplayDelimiters(string line)
    {
        var count = 0;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == '$' && line[i + 1] == '$') { count++; i++; }
        }
        return count;
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
