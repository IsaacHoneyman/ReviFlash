using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ReviFlash.Utilities;

/// <summary> Turns an Anki field (HTML, MathJax, [latex]...[/latex], {{c1::...}} clozes) into card text; images become "[image]". </summary>
public static partial class AnkiTextConverter
{
    public const string ImagePlaceholder = "[image]";

    public static string Convert(string field, out bool hadImage)
    {
        var maths = new List<string>();
        var text = ConvertClozes(field);
        text = ExtractLatexBlocks(text, maths);
        text = ExtractMaths(text, maths);
        text = ConvertHtml(text, maths, out hadImage);
        text = RestoreMaths(text, maths);
        text = ClozeOpenMarkRegex().Replace(text, @"\C$1{").Replace(ClozeClose, '}');
        return Tidy(text);
    }

    public static string Convert(string field) => Convert(field, out _);

    /// <summary> The field as plain text, for answers typed in and compared letter for letter. </summary>
    public static string ToPlainText(string field)
    {
        var text = BreakTagRegex().Replace(field, " ");
        text = TagRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    public static bool HasCloze(string field) => ClozeOpeningRegex().IsMatch(field);

    // --- Clozes: {{c1::text::hint}} -> \C1{text} (the hint is dropped) ---

    // Stand in for \C1{ and } until the HTML pass has escaped the text around them.
    private const char ClozeOpenStart = '\u000E';
    private const char ClozeOpenEnd = '\u000F';
    private const char ClozeClose = '\u0010';

    private static string ConvertClozes(string text)
    {
        var result = new StringBuilder();
        var i = 0;

        while (ClozeOpeningRegex().Match(text, i) is { Success: true } opening)
        {
            var contentStart = opening.Index + opening.Length;
            var end = FindClozeEnd(text, contentStart);
            if (end < 0) break;

            var content = ConvertClozes(text[contentStart..end]);
            var hint = TopLevelHintSeparator(content);
            if (hint >= 0) content = content[..hint];

            result.Append(text, i, opening.Index - i);
            result.Append(ClozeOpenStart).Append(opening.Groups[1].Value).Append(ClozeOpenEnd).Append(content).Append(ClozeClose);
            i = end + 2;
        }

        result.Append(text, i, text.Length - i);
        return result.ToString();
    }

    /// <summary> The index of the "}}" closing a cloze whose text starts at <paramref name="start"/>, allowing nested clozes. </summary>
    private static int FindClozeEnd(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length - 1; i++)
        {
            if (text[i] == '{' && text[i + 1] == '{') { depth++; i++; }
            else if (text[i] == '}' && text[i + 1] == '}')
            {
                if (depth == 0) return i;
                depth--;
                i++;
            }
        }
        return -1;
    }

    private static int TopLevelHintSeparator(string content)
    {
        var depth = 0;
        for (var i = 0; i < content.Length - 1; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}') depth--;
            else if (depth == 0 && content[i] == ':' && content[i + 1] == ':') return i;
        }
        return -1;
    }

    // --- Maths: taken out before the HTML is converted, so its backslashes and braces are left alone ---

    private const char MathStart = '\u0002';
    private const char MathEnd = '\u0003';

    private static string ExtractMaths(string text, List<string> maths) =>
        MathRegex().Replace(text, match =>
        {
            var display = match.Groups["display"].Success || match.Groups["display2"].Success;
            var source = new[] { "inline", "display", "inline2", "display2" }
                .Select(name => match.Groups[name]).First(group => group.Success).Value;
            var delimiter = display ? "$$" : "$";
            maths.Add(delimiter + CleanMaths(source) + delimiter);
            return $"{MathStart}{maths.Count - 1}{MathEnd}";
        });

    /// <summary> Maths as MathJax would read it: tags out, entities decoded (so &amp; is a column again). </summary>
    private static string CleanMaths(string source)
    {
        var text = BreakTagRegex().Replace(source, " ");
        text = TagRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        return text.Trim();
    }

    // --- [latex]...[/latex]: a LaTeX document fragment, text with $...$ maths inside ---

    /// <summary> Converts each [latex] block whole and sets it aside like maths, so the HTML pass leaves it alone. </summary>
    private static string ExtractLatexBlocks(string text, List<string> converted) =>
        LatexBlockRegex().Replace(text, match =>
        {
            converted.Add(ConvertLatexBlock(match.Groups[1].Value));
            return $"{MathStart}{converted.Count - 1}{MathEnd}";
        });

    private const char InnerMathStart = '\u0004';
    private const char InnerMathEnd = '\u0005';

    private static string ConvertLatexBlock(string source)
    {
        // Anki strips the editor's HTML before running LaTeX; line breaks in it are just spaces to LaTeX.
        var text = BreakTagRegex().Replace(source, "\n");
        text = TagRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');

        var maths = new List<string>();
        text = LatexMathRegex().Replace(text, match =>
        {
            var display = match.Groups["display"].Success || match.Groups["display2"].Success;
            var latex = new[] { "display", "display2", "inline", "inline2" }
                .Select(name => match.Groups[name]).First(group => group.Success).Value.Trim();
            var delimiter = display ? "$$" : "$";
            maths.Add(delimiter + latex + delimiter);
            return $"{InnerMathStart}{maths.Count - 1}{InnerMathEnd}";
        });

        text = LatexCommentRegex().Replace(text, "");
        // A blank line is a new paragraph; any other line break is a space.
        text = BlankLineRegex().Replace(text, "\u0001");
        text = text.Replace('\n', ' ').Replace("\u0001", "\n\n");
        text = LatexLineBreakRegex().Replace(text, "\n");
        text = LatexListRegex().Replace(text, "\n");
        text = LatexItemRegex().Replace(text, "\n- ");

        foreach (var (command, replacement) in LatexTextCommands)
            text = ReplaceCommand(text, command, replacement);

        foreach (var (escaped, literal) in LatexEscapes)
            text = text.Replace(escaped, literal);

        // Drop any other command (\large, \centering...) and braces that only grouped text, shielding literal \{ and \} meanwhile.
        text = text.Replace(@"\{", "\u0006").Replace(@"\}", "\u0007");
        text = UnknownCommandRegex().Replace(text, "");
        text = RemoveGroupingBraces(text).Replace('\u0006', '{').Replace('\u0007', '}');

        return InnerMathRegex().Replace(text, match => maths[int.Parse(match.Groups[1].Value)]);
    }

    /// <summary> Text commands and what they become: \B{ / \I{ / \U{ / \T{ / \H1{ for styling, or a plain group to unwrap. </summary>
    private static readonly (string Command, string Replacement)[] LatexTextCommands =
    [
        ("section", @"\H1{"), ("subsection", @"\H2{"), ("subsubsection", @"\H3{"),
        ("textbf", @"\B{"), ("textit", @"\I{"), ("emph", @"\I{"), ("textsl", @"\I{"), ("underline", @"\U{"),
        ("texttt", @"\T{"), ("textrm", "{"), ("textsf", "{"), ("textup", "{"), ("textnormal", "{"), ("text", "{"), ("mbox", "{"),
    ];

    private static readonly (string Escaped, string Literal)[] LatexEscapes =
    [
        ("---", "—"), ("--", "–"), ("``", "“"), ("''", "”"), ("~", " "),
        (@"\%", "%"), (@"\&", "&"), (@"\_", "_"), (@"\#", "#"), (@"\ldots", "…"), (@"\dots", "…"),
    ];

    private static string ReplaceCommand(string text, string command, string replacement) =>
        Regex.Replace(text, $@"\\{command}\s*\{{", replacement.Replace("$", "$$"));

    /// <summary> Removes {...} pairs that aren't the brace of \B{, \I{, \U{, \H1{ or a cloze \C1{. </summary>
    private static string RemoveGroupingBraces(string text)
    {
        var output = new StringBuilder();
        var kept = new Stack<bool>();

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '{')
            {
                var keep = OurCommandBefore(text, i);
                kept.Push(keep);
                if (keep) output.Append(c);
            }
            else if (c == '}')
            {
                if (kept.Count == 0 || kept.Pop()) output.Append(c);
            }
            else
            {
                output.Append(c);
            }
        }

        return output.ToString();
    }

    private static bool OurCommandBefore(string text, int brace) =>
        OurCommandRegex().IsMatch(text[Math.Max(0, brace - 8)..brace]);

    private static string RestoreMaths(string text, List<string> maths) =>
        MathPlaceholderRegex().Replace(text, match => maths[int.Parse(match.Groups[1].Value)]);

    private static string ConvertHtml(string html, List<string> maths, out bool hadImage)
    {
        hadImage = false;
        var output = new StringBuilder();
        // Open tags, what each one closes with, and where its opening and content start in the output.
        var open = new List<(string Tag, string Closing, int Start, int ContentStart)>();
        var i = 0;

        void NewLine()
        {
            if (output.Length > 0 && output[^1] != '\n') output.Append('\n');
        }

        // A "- " with nothing after it yet.
        bool AtBareBullet() => output.Length >= 2 && output[^1] == ' ' && output[^2] == '-' && (output.Length == 2 || output[^3] == '\n');

        void Close((string Tag, string Closing, int Start, int ContentStart) entry)
        {
            if (entry.Tag is "sup" or "sub" && output.ToString(entry.ContentStart, output.Length - entry.ContentStart) is var inner && inner.Contains(MathStart))
            {
                output.Length = entry.Start;
                output.Append(MathScript(entry.Tag == "sup" ? '^' : '_', inner, maths));
            }
            else
            {
                output.Append(entry.Closing);
            }
        }

        foreach (Match tag in HtmlTokenRegex().Matches(html))
        {
            AppendText(output, html[i..tag.Index]);
            i = tag.Index + tag.Length;

            if (tag.Groups["comment"].Success) continue;

            var name = tag.Groups["name"].Value.ToLowerInvariant();
            var closing = tag.Groups["close"].Success;
            var attributes = tag.Groups["attrs"].Value;

            if (closing)
            {
                var index = open.FindLastIndex(entry => entry.Tag == name);
                if (index >= 0)
                {
                    // Close anything left open inside it too, innermost first.
                    for (var j = open.Count - 1; j >= index; j--) Close(open[j]);
                    open.RemoveRange(index, open.Count - index);
                }
                if (name == "li" && AtBareBullet()) output.Length -= 2;
                if (IsBlock(name)) NewLine();
                continue;
            }

            switch (name)
            {
                case "br":
                    if (!AtBareBullet()) output.Append('\n');
                    continue;
                case "hr":
                    NewLine();
                    output.Append('\n');
                    continue;
                case "img":
                    hadImage = true;
                    output.Append(ImagePlaceholder);
                    continue;
                case "li":
                    if (!AtBareBullet())
                    {
                        NewLine();
                        output.Append("- ");
                    }
                    break;
                case "td" or "th":
                    if (output.Length > 0 && output[^1] is not ('\n' or ' ')) output.Append("   ");
                    break;
                default:
                    if (IsBlock(name) && !AtBareBullet()) NewLine();
                    break;
            }

            var (opening, closingText) = Formatting(name, attributes);
            var start = output.Length;
            output.Append(opening);
            if (!tag.Groups["self"].Success) open.Add((name, closingText, start, output.Length));
        }

        AppendText(output, html[i..]);
        for (var j = open.Count - 1; j >= 0; j--) Close(open[j]);
        return output.ToString();
    }

    /// <summary> Superscript or subscript holding maths: the maths as it is, the text around it in \text. </summary>
    private static string MathScript(char operatorChar, string content, List<string> maths)
    {
        var body = new StringBuilder();
        var position = 0;

        void AppendWords(string words)
        {
            if (words.Length > 0) body.Append(@"\text{").Append(words).Append('}');
        }

        foreach (Match placeholder in MathPlaceholderRegex().Matches(content))
        {
            AppendWords(content[position..placeholder.Index]);
            var source = maths[int.Parse(placeholder.Groups[1].Value)];
            body.Append(source switch
            {
                ['$', '$', .. var display, '$', '$'] => display,
                ['$', .. var inline, '$'] => inline,
                _ => $@"\text{{{source}}}",
            });
            position = placeholder.Index + placeholder.Length;
        }

        AppendWords(content[position..]);
        return $"${operatorChar}{{{body}}}$";
    }

    private static bool IsBlock(string tag) =>
        tag is "div" or "p" or "li" or "ul" or "ol" or "tr" or "table" or "blockquote" or "pre"
            or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    /// <summary> What a tag opens and closes in card text: \B{ \I{ \U{ for styling, \H1{ to \H3{ for headings, $^{...}$ / $_{...}$ for sup and sub. </summary>
    private static (string Opening, string Closing) Formatting(string tag, string attributes)
    {
        switch (tag)
        {
            case "sup": return (@"$^{\text{", "}}$");
            case "sub": return (@"$_{\text{", "}}$");
        }

        var heading = tag is "h1" or "h2" or "h3" ? $@"\H{tag[1]}{{" : "";
        var bold = tag is "b" or "strong" or "h4" or "h5" or "h6";
        var italic = tag is "i" or "em";
        var underline = tag == "u";
        var code = tag is "code" or "tt" or "kbd" or "samp";

        if (StyleRegex().Match(attributes) is { Success: true } style)
        {
            var css = style.Groups[1].Value.ToLowerInvariant();
            bold |= BoldStyleRegex().IsMatch(css);
            italic |= css.Contains("font-style: italic") || css.Contains("font-style:italic");
            underline |= UnderlineStyleRegex().IsMatch(css);
        }

        var opening = heading + (bold ? @"\B{" : "") + (italic ? @"\I{" : "") + (underline ? @"\U{" : "") + (code ? @"\T{" : "");
        return (opening, new string('}', opening.Count(c => c == '{')));
    }

    /// <summary> Text between tags: entities decoded, $, unmatched braces and backslashes that could be read as markup escaped (MathJax was already taken out), and [sound:...] dropped. </summary>
    private static void AppendText(StringBuilder output, string text)
    {
        if (text.Length == 0) return;
        text = SoundRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        // Newlines in Anki's HTML source aren't shown; only tags break lines.
        text = text.Replace("\r", "").Replace('\n', ' ');
        output.Append(EscapeText(text));
    }

    private static string EscapeText(string text)
    {
        // Braces that pair up within the text are plain; the rest would pair with the format around them.
        var unmatched = new HashSet<int>();
        var openBraces = new Stack<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{') openBraces.Push(i);
            else if (text[i] == '}' && !openBraces.TryPop(out _)) unmatched.Add(i);
        }
        unmatched.UnionWith(openBraces);

        var output = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var escape = c switch
            {
                '$' => true,
                '{' or '}' => unmatched.Contains(i),
                '\\' => i + 1 >= text.Length || !char.IsAsciiLetterOrDigit(text[i + 1]) || text[i + 1] is 'B' or 'I' or 'U' or 'T' or 'H' or 'C',
                _ => false,
            };
            if (escape) output.Append('\\');
            output.Append(c);
        }
        return output.ToString();
    }

    private static string Tidy(string text)
    {
        // Styling left with nothing in it, e.g. <b></b> or <b><br></b>.
        string previous;
        do
        {
            previous = text;
            text = EmptyFormatRegex().Replace(text, "");
        }
        while (text != previous);

        var lines = text.Split('\n').Select(line => MultipleSpacesRegex().Replace(line, " ").Trim());
        text = string.Join('\n', lines);
        text = ManyNewLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex("\u000E(\\d*)\u000F")]
    private static partial Regex ClozeOpenMarkRegex();

    [GeneratedRegex(@"\{\{c(\d+)::")]
    private static partial Regex ClozeOpeningRegex();

    // MathJax as Anki writes it (\(...\) and \[...\]) and its older [$]...[/$] and [$$]...[/$$] tags.
    [GeneratedRegex(@"\\\((?<inline>.+?)\\\)|\\\[(?<display>.+?)\\\]|\[\$\$\](?<display2>.+?)\[/\$\$\]|\[\$\](?<inline2>.+?)\[/\$\]", RegexOptions.Singleline)]
    private static partial Regex MathRegex();

    [GeneratedRegex(@"\[latex\](.+?)\[/latex\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LatexBlockRegex();

    // Inside [latex]: $$...$$, \[...\], $...$ and \(...\), but not an escaped \$.
    [GeneratedRegex(@"\$\$(?<display>.+?)\$\$|\\\[(?<display2>.+?)\\\]|(?<!\\)\$(?<inline>.+?)(?<!\\)\$|\\\((?<inline2>.+?)\\\)", RegexOptions.Singleline)]
    private static partial Regex LatexMathRegex();

    [GeneratedRegex("\u0004(\\d+)\u0005")]
    private static partial Regex InnerMathRegex();

    [GeneratedRegex(@"(?<!\\)%[^\n]*")]
    private static partial Regex LatexCommentRegex();

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex BlankLineRegex();

    [GeneratedRegex(@"\\\\(\[[^\]]*\])?\s*")]
    private static partial Regex LatexLineBreakRegex();

    [GeneratedRegex(@"\\(begin|end)\{(itemize|enumerate|description|center|flushleft|flushright)\}\s*")]
    private static partial Regex LatexListRegex();

    [GeneratedRegex(@"\s*\\item\b\s*")]
    private static partial Regex LatexItemRegex();

    // Commands other than ours (\B, \I, \U, \T, \H1, \C1) and the $ escape.
    [GeneratedRegex(@"\\(?![BIUT]\{|H[1-3]\{|C\d*\{)[a-zA-Z]+\*?\s*")]
    private static partial Regex UnknownCommandRegex();

    [GeneratedRegex(@"\\(?:[BIUT]|H[1-3]|C\d*)$")]
    private static partial Regex OurCommandRegex();

    [GeneratedRegex("\u0002(\\d+)\u0003")]
    private static partial Regex MathPlaceholderRegex();

    // Not backtracking, so a "<" with no ">" after it costs one pass over the text rather than one per "<".
    private static readonly Regex HtmlToken = new(@"<!--.*?-->(?<comment>)|<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*?)(?<self>/)?>", RegexOptions.Singleline | RegexOptions.NonBacktracking);

    private static Regex HtmlTokenRegex() => HtmlToken;

    [GeneratedRegex(@"style\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex StyleRegex();

    [GeneratedRegex(@"font-weight\s*:\s*(bold|bolder|[6-9]00)")]
    private static partial Regex BoldStyleRegex();

    [GeneratedRegex(@"text-decoration(-line)?\s*:[^;]*underline")]
    private static partial Regex UnderlineStyleRegex();

    [GeneratedRegex(@"\[sound:[^\]]*\]")]
    private static partial Regex SoundRegex();

    [GeneratedRegex(@"<br\s*/?>|</?(div|p|li|tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTagRegex();

    private static readonly Regex Tag = new(@"<[^>]*>", RegexOptions.NonBacktracking);

    private static Regex TagRegex() => Tag;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewLinesRegex();

    [GeneratedRegex(@"\\(?:[BIUT]|H[1-3])\{\s*\}")]
    private static partial Regex EmptyFormatRegex();
}
