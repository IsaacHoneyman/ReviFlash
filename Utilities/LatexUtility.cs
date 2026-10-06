using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReviFlash.Utilities;

public abstract record CardSegment;

/// <summary>
/// Literal text, styled by any \B{...} / \I{...} / \U{...} it sits in, and in a heading (1 to 3) when inside \H1{...} to \H3{...}.
/// </summary>
public sealed record TextSegment(string Text, bool Bold, bool Italic, bool Underline = false, int Heading = 0) : CardSegment;

/// <summary> Maths ready for CSharpMath, plus what the user typed for falling back to. </summary>
public sealed record MathSegment(string Latex, string Source, bool Display, int Heading = 0) : CardSegment;

public sealed record LineBreakSegment : CardSegment;

/// <summary>
/// Turns card text into segments for rendering: styled text, line breaks, and maths ready for CSharpMath.
/// Outside $...$ / $$...$$ everything is literal apart from \B{...}, \I{...}, \U{...}, headings \H1{...} to \H3{...}, \$
/// and cloze blanks \C{...} (shown bold);
/// inside, the maths is passed through with rewrites for commands CSharpMath lacks.
/// </summary>
public static partial class LatexUtility
{
    // --- Regex ---

    [GeneratedRegex(@"\\(?:big|Big|bigg|Bigg)[lrm]?(?![a-zA-Z])")]
    private static partial Regex SizedDelimiterRegex();

    [GeneratedRegex(@"\\C\d*\{")]
    private static partial Regex ClozeOpeningRegex();

    [GeneratedRegex(@"\\pmod\s*\{([^{}]*)\}")]
    private static partial Regex PmodRegex();

    [GeneratedRegex(@"\\not\s*(\\in(?![a-zA-Z])|=)")]
    private static partial Regex NegationRegex();

    [GeneratedRegex(@"\\(p?matrix)\s*\{")]
    private static partial Regex OldMatrixRegex();

    [GeneratedRegex(@"\\begin\{(matrix|pmatrix|bmatrix|Bmatrix|vmatrix|Vmatrix|array|aligned)\}")]
    private static partial Regex TableBeginRegex();

    [GeneratedRegex(@"(\\[a-zA-Z]+)\*")]
    private static partial Regex StarredCommandRegex();

    [GeneratedRegex(@"\\([a-zA-Z]+)(?![a-zA-Z])")]
    private static partial Regex MathCommandRegex();

    /// <summary> Maths commands CSharpMath doesn't know, mapped to ones it renders the same or close. </summary>
    private static readonly Dictionary<string, string> MathRewrites = new()
    {
        ["B"] = @"\mathbf",
        ["I"] = @"\mathit",
        ["U"] = @"\underline",
        ["dfrac"] = @"\frac",
        ["tfrac"] = @"\frac",
        ["textbf"] = @"\mathbf",
        ["textit"] = @"\mathit",
        ["emph"] = @"\mathit",
        ["textrm"] = @"\text",
        ["lvert"] = "|",
        ["rvert"] = "|",
        ["lVert"] = @"\|",
        ["rVert"] = @"\|",
        ["impliedby"] = @"\Leftarrow",
        ["owns"] = @"\ni",
        ["lnot"] = @"\neg",
        ["prime"] = "'",
        ["bmod"] = @"\;\mathrm{mod}\;",
        // AvaloniaMath spellings found in existing decks.
        ["mod"] = @"\mathrm{mod}\,",
        ["cosec"] = @"\operatorname{cosec}",
        ["lbrack"] = "[",
        ["rbrack"] = "]",
    };

    /// <summary> Splits card text into styled text, maths and line breaks. </summary>
    public static List<CardSegment> Parse(string? input)
    {
        var segments = new List<CardSegment>();
        if (string.IsNullOrEmpty(input)) return segments;

        var text = new StringBuilder();
        // One entry per open brace in the text: 'B' / 'I' / 'U' for a \B{ / \I{ / \U{ group, '1' to '3' for a
        // heading, '{' for a literal brace.
        var braces = new Stack<char>();
        var i = 0;

        // The innermost heading wins (a stack enumerates from the top).
        int Heading() => braces.FirstOrDefault(char.IsAsciiDigit) is var level and not '\0' ? level - '0' : 0;

        void FlushText()
        {
            if (text.Length == 0) return;
            segments.Add(new TextSegment(text.ToString(), braces.Contains('B'), braces.Contains('I'), braces.Contains('U'), Heading()));
            text.Clear();
        }

        while (i < input.Length)
        {
            var c = input[i];

            if (c == '$')
            {
                var display = i + 1 < input.Length && input[i + 1] == '$';
                var delimiter = display ? "$$" : "$";
                var start = i + delimiter.Length;
                var end = FindClosingDollar(input, start, delimiter);

                if (end > start)
                {
                    FlushText();
                    var source = input[start..end];
                    segments.Add(new MathSegment(ToMathLatex(source), source, display, Heading()));
                    i = end + delimiter.Length;
                    continue;
                }

                // An unclosed $ is just a dollar sign.
                text.Append('$');
                i++;
                continue;
            }

            if (c == '\\')
            {
                if (i + 1 < input.Length && input[i + 1] == '$')
                {
                    text.Append('$');
                    i += 2;
                    continue;
                }

                // A cloze blank's text shows in bold wherever the whole card is shown (editor, card list).
                if (ClozeUtility.TryReadOpening(input, i, out _, out var blankStart))
                {
                    FlushText();
                    braces.Push('B');
                    i = blankStart;
                    continue;
                }

                if (TryReadFormatCommand(input, i, out var command))
                {
                    FlushText();
                    braces.Push(command);
                    i += 3;
                    continue;
                }

                if (TryReadHeading(input, i, out var level))
                {
                    FlushText();
                    braces.Push(level);
                    i += 4;
                    continue;
                }
            }

            if (c == '}' && braces.Count > 0)
            {
                if (braces.Peek() == '{') text.Append('}');
                else FlushText();
                braces.Pop();
                i++;
                continue;
            }

            if (c == '{') braces.Push('{');

            if (c == '\n')
            {
                FlushText();
                segments.Add(new LineBreakSegment());
            }
            else if (c != '\r')
            {
                text.Append(c);
            }
            i++;
        }

        // Any \B{ / \I{ the user left open just runs to the end of the card.
        FlushText();
        return segments;
    }

    public static string ToMathLatex(string math)
    {
        var result = math.Replace("\r", "").Replace('\n', ' ').Replace(@"\/", "");
        result = ClozeOpeningRegex().Replace(result, @"\mathbf{");
        result = SizedDelimiterRegex().Replace(result, "");
        result = PmodRegex().Replace(result, @"\;(\mathrm{mod}\;$1)");
        result = NegationRegex().Replace(result, match => match.Groups[1].Value == "=" ? @"\neq" : @"\notin");
        result = StarredCommandRegex().Replace(result, "$1{}*");
        result = RewriteOldMatrices(result);
        result = PadTables(result);
        return MathCommandRegex().Replace(result, match =>
            MathRewrites.TryGetValue(match.Groups[1].Value, out var rewrite) ? rewrite : match.Value);
    }

    /// <summary> AvaloniaMath's \matrix{a & b \\ c & d} and \pmatrix{...} become matrix environments. </summary>
    private static string RewriteOldMatrices(string math)
    {
        // Innermost (last) first, so a matrix nested in another is rewritten before its parent.
        var matches = OldMatrixRegex().Matches(math);
        for (var m = matches.Count - 1; m >= 0; m--)
        {
            var match = matches[m];
            var bodyStart = match.Index + match.Length;
            var bodyEnd = FindClosingBrace(math, bodyStart);
            if (bodyEnd < 0) continue;

            var environment = match.Groups[1].Value;
            var body = math[bodyStart..bodyEnd].Replace(@"\cr", @"\\");
            math = math[..match.Index] + $@"\begin{{{environment}}}{body}\end{{{environment}}}" + math[(bodyEnd + 1)..];
        }
        return math;
    }

    /// <summary>
    /// CSharpMath measures each table row from its first cell's left edge, so when that cell is centred
    /// or right-aligned in a wider column the table comes out too narrow and following content overlaps it.
    /// An empty first column pins every row to x = 0, and an empty last column keeps the padding even.
    /// </summary>
    private static string PadTables(string math)
    {
        var begins = TableBeginRegex().Matches(math);

        // Last first: nothing after it has been rewritten yet, and inner tables are done before outer ones.
        for (var m = begins.Count - 1; m >= 0; m--)
        {
            var begin = begins[m];
            var environment = begin.Groups[1].Value;
            var bodyStart = begin.Index + begin.Length;

            var spec = "";
            if (environment == "array")
            {
                var specOpen = SkipSpaces(math, bodyStart);
                if (specOpen >= math.Length || math[specOpen] != '{') continue;
                var specClose = FindClosingBrace(math, specOpen + 1);
                if (specClose < 0) continue;
                spec = math[(specOpen + 1)..specClose];
                bodyStart = specClose + 1;
            }

            var end = FindEnvironmentEnd(math, bodyStart, environment);
            if (end < 0) continue;
            var endTag = $@"\end{{{environment}}}";
            var rows = SplitTopLevel(math[bodyStart..end], @"\\");
            if (rows.Count > 1 && string.IsNullOrWhiteSpace(rows[^1])) rows.RemoveAt(rows.Count - 1);

            if (environment == "aligned")
            {
                // aligned only allows 2 columns, so it becomes the array it lays out as: right, then left
                // with an empty atom before the second cell for the spacing around =.
                for (var r = 0; r < rows.Count; r++)
                {
                    var cells = SplitTopLevel(rows[r], "&");
                    if (cells.Count > 1) rows[r] = $"{cells[0]}&{{}}{string.Join("&", cells.Skip(1))}";
                }
                environment = "array";
                spec = "rl";
            }

            var body = string.Join(@"\\", rows.Select(row => $"&{row}&"));
            var replacement = environment switch
            {
                "array" => $@"\begin{{array}}{{c{spec}c}}{body}\end{{array}}",
                "matrix" => $@"\begin{{matrix}}{body}\end{{matrix}}",
                // The bracketed environments lose the padding column on one side; \left \right keeps it even.
                _ => $@"\left{MatrixDelimiters[environment].Left}\begin{{matrix}}{body}\end{{matrix}}\right{MatrixDelimiters[environment].Right}",
            };

            math = math[..begin.Index] + replacement + math[(end + endTag.Length)..];
        }
        return math;
    }

    private static readonly Dictionary<string, (string Left, string Right)> MatrixDelimiters = new()
    {
        ["pmatrix"] = ("(", ")"),
        ["bmatrix"] = ("[", "]"),
        ["Bmatrix"] = (@"\{", @"\}"),
        ["vmatrix"] = ("|", "|"),
        ["Vmatrix"] = (@"\|", @"\|"),
    };

    /// <summary> Index of the \end{environment} matching a \begin whose body starts at start, or -1. </summary>
    private static int FindEnvironmentEnd(string text, int start, string environment)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] != '\\') continue;
            if (string.CompareOrdinal(text, i, @"\begin{", 0, 7) == 0) depth++;
            else if (string.CompareOrdinal(text, i, @"\end{", 0, 5) == 0)
            {
                if (depth == 0)
                    return string.CompareOrdinal(text, i, $@"\end{{{environment}}}", 0, environment.Length + 6) == 0 ? i : -1;
                depth--;
            }
            else i++; // skip the escaped character, e.g. \\ or \{
        }
        return -1;
    }

    /// <summary> Splits on a separator that is outside braces and nested environments. </summary>
    private static List<string> SplitTopLevel(string text, string separator)
    {
        var parts = new List<string>();
        var braceDepth = 0;
        var environmentDepth = 0;
        var partStart = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (braceDepth == 0 && environmentDepth == 0 && string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
            {
                parts.Add(text[partStart..i]);
                i += separator.Length - 1;
                partStart = i + 1;
                continue;
            }

            if (text[i] == '\\')
            {
                if (string.CompareOrdinal(text, i, @"\begin{", 0, 7) == 0) environmentDepth++;
                else if (string.CompareOrdinal(text, i, @"\end{", 0, 5) == 0) environmentDepth--;
                else i++;
            }
            else if (text[i] == '{') braceDepth++;
            else if (text[i] == '}') braceDepth--;
        }

        parts.Add(text[partStart..]);
        return parts;
    }

    private static int SkipSpaces(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
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

    /// <summary> \B{, \I{ or \U{, with the letter not part of a longer command name. </summary>
    private static bool TryReadFormatCommand(string input, int index, out char command)
    {
        command = default;
        if (index + 2 >= input.Length) return false;
        var name = input[index + 1];
        if ((name != 'B' && name != 'I' && name != 'U') || input[index + 2] != '{') return false;
        command = name;
        return true;
    }

    /// <summary> \H1{, \H2{ or \H3{, giving the level as its digit. </summary>
    private static bool TryReadHeading(string input, int index, out char level)
    {
        level = default;
        if (index + 3 >= input.Length || input[index + 1] != 'H' || input[index + 3] != '{') return false;
        if (input[index + 2] is not ('1' or '2' or '3')) return false;
        level = input[index + 2];
        return true;
    }

    private static int FindClosingDollar(string input, int start, string delimiter)
    {
        var index = start;
        while (index < input.Length)
        {
            var found = input.IndexOf(delimiter, index, System.StringComparison.Ordinal);
            if (found < 0) return -1;
            if (input[found - 1] != '\\') return found;
            index = found + 1;
        }
        return -1;
    }
}
