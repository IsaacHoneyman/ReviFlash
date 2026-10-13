using System.Diagnostics;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class LatexUtilityTests
{
    private static string Text(string input) =>
        string.Concat(LatexUtility.Parse(input).OfType<TextSegment>().Select(segment => segment.Text));

    [Fact]
    public void Parse_returns_nothing_for_empty_input()
    {
        Assert.Empty(LatexUtility.Parse(null));
        Assert.Empty(LatexUtility.Parse(""));
    }

    [Fact]
    public void Parse_styles_bold_italic_and_underline()
    {
        var segments = LatexUtility.Parse(@"a \B{b} \I{c} \U{d}").OfType<TextSegment>().ToList();

        Assert.Equal(["a ", "b", " ", "c", " ", "d"], segments.Select(s => s.Text));
        Assert.True(segments[1].Bold);
        Assert.True(segments[3].Italic);
        Assert.True(segments[5].Underline);
        Assert.False(segments[0].Bold);
    }

    [Fact]
    public void Parse_nests_formats()
    {
        var segment = LatexUtility.Parse(@"\B{\I{x}}").OfType<TextSegment>().Single();

        Assert.True(segment.Bold);
        Assert.True(segment.Italic);
    }

    [Fact]
    public void Parse_reads_headings_and_the_innermost_wins()
    {
        var segments = LatexUtility.Parse(@"\H1{a \H2{b} c}").OfType<TextSegment>().ToList();

        Assert.Equal([1, 2, 1], segments.Select(s => s.Heading));
    }

    [Fact]
    public void Parse_reads_bullets_and_line_breaks()
    {
        var segments = LatexUtility.Parse("- one\ntwo");

        Assert.Equal(new TextSegment("one", false, false, false, 0, true), segments[0]);
        Assert.IsType<LineBreakSegment>(segments[1]);
        Assert.False(((TextSegment)segments[2]).Bullet);
    }

    [Fact]
    public void Parse_reads_escaped_dollar_and_dash_as_text()
    {
        Assert.Equal("$5 - 3", Text(@"\$5 \- 3"));
        var segments = LatexUtility.Parse(@"\- not a bullet");
        Assert.False(((TextSegment)segments[0]).Bullet);
    }

    [Fact]
    public void Parse_reads_inline_and_display_maths()
    {
        var segments = LatexUtility.Parse("a $x^2$ b $$y$$").OfType<MathSegment>().ToList();

        Assert.Equal(["x^2", "y"], segments.Select(s => s.Source));
        Assert.Equal([false, true], segments.Select(s => s.Display));
    }

    [Fact]
    public void Parse_treats_an_unclosed_dollar_as_text()
    {
        Assert.Equal("cost $5", Text("cost $5"));
    }

    [Fact]
    public void Parse_shows_cloze_blank_text_in_bold()
    {
        var segments = LatexUtility.Parse(@"a \C1{b}").OfType<TextSegment>().ToList();

        Assert.Equal(["a ", "b"], segments.Select(s => s.Text));
        Assert.True(segments[1].Bold);
    }

    [Fact]
    public void Parse_keeps_balanced_plain_braces()
    {
        Assert.Equal("a {b} c", Text("a {b} c"));
    }

    [Fact]
    public void Parse_reads_escaped_braces_as_text_without_touching_formatting()
    {
        var segments = LatexUtility.Parse(@"\B{\{x} y").OfType<TextSegment>().ToList();

        Assert.Equal(["{x", " y"], segments.Select(s => s.Text));
        Assert.True(segments[0].Bold);
        Assert.False(segments[1].Bold);
    }

    [Fact]
    public void Parse_reads_an_escaped_backslash_as_one_backslash()
    {
        Assert.Equal(@"a\b", Text(@"a\\b"));
    }

    [Fact]
    public void Parse_leaves_other_backslashes_alone()
    {
        Assert.Equal(@"\alpha \x", Text(@"\alpha \x"));
    }

    [Fact]
    public void Parse_reads_an_escaped_command_as_literal_text()
    {
        Assert.Equal(@"\B{x}", Text(@"\\B\{x\}"));
        Assert.Equal(@"\C{x}", Text(@"\\C{x}"));
    }

    [Fact]
    public void Parse_agrees_with_ClozeUtility_on_an_escaped_backslash_before_a_blank()
    {
        const string input = @"\\C{x}";

        Assert.False(ClozeUtility.HasBlanks(input));
        Assert.All(LatexUtility.Parse(input).OfType<TextSegment>(), segment => Assert.False(segment.Bold));
        Assert.Equal(@"\C{x}", Text(input));

        const string blank = @"\\\C{x}";
        Assert.True(ClozeUtility.HasBlanks(blank));
        Assert.Contains(LatexUtility.Parse(blank).OfType<TextSegment>(), segment => segment.Bold && segment.Text == "x");
    }

    [Fact]
    public void Parse_runs_unclosed_formats_to_the_end()
    {
        var segment = LatexUtility.Parse(@"\B{abc").OfType<TextSegment>().Single();

        Assert.True(segment.Bold);
        Assert.Equal("abc", segment.Text);
    }

    [Fact]
    public void ToMathLatex_rewrites_unknown_commands_and_cloze_blanks()
    {
        Assert.Equal(@"\frac{1}{2}", LatexUtility.ToMathLatex(@"\dfrac{1}{2}"));
        Assert.Equal(@"\mathbf{x}", LatexUtility.ToMathLatex(@"\C1{x}"));
        Assert.Equal(@"a \mathbf{b}", LatexUtility.ToMathLatex(@"a \B{b}"));
    }

    [Theory]
    [InlineData(@"\texttt{int main}", @"\mathtt{int\ main}")]
    [InlineData(@"\textsf{a b}", @"\mathsf{a\ b}")]
    [InlineData(@"x + \texttt{f(x)} = 1", @"x + \mathtt{f(x)} = 1")]
    [InlineData(@"\texttt{already\ escaped}", @"\mathtt{already\ escaped}")]
    [InlineData(@"\texttt{a {b c} d}", @"\mathtt{a\ {b\ c}\ d}")]
    [InlineData(@"\texttt{a} b \texttt{c d}", @"\mathtt{a} b \mathtt{c\ d}")]
    [InlineData(@"\texttt{unclosed x", @"\mathtt{unclosed\ x")]
    public void ToMathLatex_turns_text_fonts_into_maths_fonts_that_keep_spaces(string input, string expected)
    {
        Assert.Equal(expected, LatexUtility.ToMathLatex(input));
    }

    [Fact]
    public void ToMathLatex_pads_matrices()
    {
        Assert.Contains(@"\begin{matrix}&a&b&\\&c&d&\end{matrix}", LatexUtility.ToMathLatex(@"\begin{matrix}a&b\\c&d\end{matrix}").Replace(" ", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_handles_deep_nesting_in_linear_time()
    {
        const int depth = 30_000;
        var deepBraces = new string('{', depth) + string.Concat(Enumerable.Repeat("a\n", depth));
        var deepFormats = string.Concat(Enumerable.Repeat(@"\B{a", depth));
        var clock = Stopwatch.StartNew();

        Assert.NotEmpty(LatexUtility.Parse(deepBraces));
        Assert.NotEmpty(LatexUtility.Parse(deepFormats));

        Assert.True(clock.ElapsedMilliseconds < 500, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(@"$E[X] \\$", @"E[X] \\", false)]
    [InlineData(@"$$a \\ b \\$$", @"a \\ b \\", true)]
    public void Parse_closes_maths_after_an_escaped_backslash(string input, string source, bool display)
    {
        var maths = Assert.IsType<MathSegment>(Assert.Single(LatexUtility.Parse(input)));
        Assert.Equal(source, maths.Source);
        Assert.Equal(display, maths.Display);
    }

    [Fact]
    public void Parse_keeps_an_escaped_dollar_inside_maths()
    {
        var maths = Assert.IsType<MathSegment>(Assert.Single(LatexUtility.Parse(@"$a \$ b$")));
        Assert.Equal(@"a \$ b", maths.Source);
    }
}
