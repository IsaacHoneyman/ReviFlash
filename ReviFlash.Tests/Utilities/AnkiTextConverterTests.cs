using System.Diagnostics;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class AnkiTextConverterTests
{
    private static string Convert(string field) => AnkiTextConverter.Convert(field);

    private static string Rendered(string field) =>
        string.Concat(LatexUtility.Parse(Convert(field)).OfType<TextSegment>().Select(segment => segment.Text));

    // --- current behaviour ---

    [Fact]
    public void Plain_text_stays_plain()
    {
        Assert.Equal("hello world", Convert("hello world"));
    }

    [Theory]
    [InlineData("<b>x</b>", @"\B{x}")]
    [InlineData("<strong>x</strong>", @"\B{x}")]
    [InlineData("<i>x</i>", @"\I{x}")]
    [InlineData("<em>x</em>", @"\I{x}")]
    [InlineData("<u>x</u>", @"\U{x}")]
    [InlineData("<h1>x</h1>", @"\H1{x}")]
    [InlineData("<h2>x</h2>", @"\H2{x}")]
    [InlineData("<b><i>x</i></b>", @"\B{\I{x}}")]
    [InlineData(@"<span style=""font-weight: bold"">x</span>", @"\B{x}")]
    [InlineData(@"<span style=""font-style:italic"">x</span>", @"\I{x}")]
    [InlineData(@"<span style=""text-decoration: underline"">x</span>", @"\U{x}")]
    public void Formatting_tags_become_commands(string html, string expected)
    {
        Assert.Equal(expected, Convert(html));
    }

    [Fact]
    public void Breaks_and_blocks_become_line_breaks()
    {
        Assert.Equal("a\nb", Convert("a<br>b"));
        Assert.Equal("a\nb", Convert("<div>a</div><div>b</div>"));
        Assert.Equal("a\n\nb", Convert("a<br><br>b"));
    }

    [Fact]
    public void Entities_are_decoded_and_dollars_escaped()
    {
        Assert.Equal(@"a & b < c", Convert("a &amp; b &lt; c"));
        Assert.Equal(@"cost \$5", Convert("cost $5"));
        Assert.Equal("a b", Convert("a&nbsp;b"));
    }

    [Fact]
    public void Images_become_placeholders()
    {
        Assert.Equal("a [image]", AnkiTextConverter.Convert(@"a <img src=""x.png"">", out var hadImage));
        Assert.True(hadImage);
        AnkiTextConverter.Convert("a", out var none);
        Assert.False(none);
    }

    [Fact]
    public void Sounds_and_comments_are_dropped()
    {
        Assert.Equal("a b", Convert("a [sound:x.mp3]<!-- hidden --> b"));
    }

    [Fact]
    public void Empty_formatting_is_removed()
    {
        Assert.Equal("a b", Convert("a <b></b>b"));
        Assert.Equal("a", Convert("<b><br></b>a"));
    }

    [Fact]
    public void Unclosed_tags_are_closed_at_the_end()
    {
        Assert.Equal(@"\B{x y}", Convert("<b>x y"));
    }

    [Fact]
    public void Simple_list_becomes_bullets()
    {
        Assert.Equal("- one\n- two", Convert("<ul><li>one</li><li>two</li></ul>"));
    }

    [Fact]
    public void Table_cells_are_spaced()
    {
        Assert.Equal("a b\nc d", Convert("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>"));
    }

    // --- maths ---

    [Theory]
    [InlineData(@"\(x^2\)", "$x^2$")]
    [InlineData(@"\[x^2\]", "$$x^2$$")]
    [InlineData("[$]x^2[/$]", "$x^2$")]
    [InlineData("[$$]x^2[/$$]", "$$x^2$$")]
    [InlineData(@"a \(x\) b", "a $x$ b")]
    public void MathJax_becomes_dollar_maths(string field, string expected)
    {
        Assert.Equal(expected, Convert(field));
    }

    [Fact]
    public void Maths_keeps_its_backslashes_braces_and_entities_decoded()
    {
        Assert.Equal(@"$\frac{a}{b} & c$", Convert(@"\(\frac{a}{b} &amp; c\)"));
    }

    [Fact]
    public void Latex_blocks_become_text_with_maths()
    {
        Assert.Equal(@"Let $x$ be \B{big}", Convert(@"[latex]Let $x$ be \textbf{big}[/latex]"));
    }

    // --- clozes ---

    [Fact]
    public void Clozes_become_numbered_blanks_and_hints_are_dropped()
    {
        Assert.Equal(@"\C1{a} and \C2{b}", Convert("{{c1::a::hint}} and {{c2::b}}"));
    }

    [Fact]
    public void Nested_clozes_nest_blanks()
    {
        Assert.Equal(@"\C1{a \C2{b} c}", Convert("{{c1::a {{c2::b}} c}}"));
    }

    [Fact]
    public void Cloze_in_maths_stays_in_the_maths()
    {
        Assert.Equal(@"$x = \C1{2}$", Convert(@"\(x = {{c1::2}}\)"));
    }

    [Fact]
    public void Cloze_with_formatting_inside()
    {
        Assert.Equal(@"\C1{\B{a}} b", Convert("{{c1::<b>a</b>}} b"));
    }

    [Fact]
    public void HasCloze_and_ToPlainText()
    {
        Assert.True(AnkiTextConverter.HasCloze("{{c3::x}}"));
        Assert.False(AnkiTextConverter.HasCloze("{{x}}"));
        Assert.Equal("a b &c", AnkiTextConverter.ToPlainText("<div>a</div><b>b</b>&nbsp;&amp;c"));
    }

    // --- lists ---

    [Theory]
    [InlineData("<ul><li><p>one</p></li><li><p>two</p></li></ul>", "- one\n- two")]
    [InlineData("<ul><li><div>one</div></li><li><div>two</div></li></ul>", "- one\n- two")]
    [InlineData("<ol><li>one<br>two</li><li>three</li></ol>", "- one\ntwo\n- three")]
    [InlineData("<ul><li>a<ul><li>b</li></ul></li><li>c</li></ul>", "- a\n- b\n- c")]
    [InlineData("<ul><li><ul><li>b</li></ul></li></ul>", "- b")]
    [InlineData("<ul><li></li><li>two</li></ul>", "- two")]
    [InlineData("<ul><li>one</li><li></li></ul>", "- one")]
    [InlineData("<ul><li><br>one</li></ul>", "- one")]
    [InlineData("<ul><li><b>x</b></li></ul>", @"- \B{x}")]
    [InlineData("<ul><li><p><b>x</b></p></li></ul>", @"- \B{x}")]
    public void List_items_become_single_bullets_without_stray_dashes(string html, string expected)
    {
        Assert.Equal(expected, Convert(html));
    }

    // --- sup and sub ---

    [Fact]
    public void Plain_superscript_and_subscript_keep_working()
    {
        Assert.Equal(@"x$^{\text{2}}$", Convert("x<sup>2</sup>"));
        Assert.Equal(@"H$_{\text{2}}$O", Convert("H<sub>2</sub>O"));
    }

    [Theory]
    [InlineData(@"x<sup>\(n\)</sup>", "x$^{n}$")]
    [InlineData(@"x<sub>\(i+1\)</sub>", "x$_{i+1}$")]
    [InlineData(@"x<sup>\(n\) th</sup>", @"x$^{n\text{ th}}$")]
    [InlineData(@"x<sup>a\(n\)b</sup>", @"x$^{\text{a}n\text{b}}$")]
    public void Superscript_and_subscript_with_maths_become_valid_maths(string html, string expected)
    {
        Assert.Equal(expected, Convert(html));
    }

    [Fact]
    public void Superscript_with_maths_renders_as_one_maths_segment()
    {
        var segments = LatexUtility.Parse(Convert(@"x<sup>\(n\)</sup>"));

        Assert.Equal("^{n}", segments.OfType<MathSegment>().Single().Source);
    }

    // --- literal text that looks like our syntax ---

    [Theory]
    [InlineData(@"\B{x}")]
    [InlineData(@"\I{x}")]
    [InlineData(@"\U{x}")]
    [InlineData(@"\C{x}")]
    [InlineData(@"\C2{x}")]
    [InlineData(@"\H1{x}")]
    [InlineData(@"\H3{x}")]
    [InlineData(@"\-x")]
    [InlineData(@"\$x")]
    [InlineData(@"a \\ b")]
    [InlineData(@"C:\dir\file")]
    [InlineData(@"\alpha and \beta")]
    [InlineData(@"\{x\}")]
    public void Literal_markup_in_anki_text_renders_literally(string text)
    {
        var segments = LatexUtility.Parse(Convert(text));

        Assert.All(segments.OfType<TextSegment>(), segment => Assert.False(segment.Bold || segment.Italic || segment.Underline || segment.Heading > 0 || segment.Bullet));
        Assert.Empty(segments.OfType<MathSegment>());
        Assert.Equal(text, Rendered(text));
    }

    [Fact]
    public void Literal_markup_is_not_a_cloze_blank()
    {
        Assert.False(ClozeUtility.HasBlanks(Convert(@"\C{x}")));
    }

    [Fact]
    public void Literal_markup_inside_formatting_renders_literally()
    {
        var segments = LatexUtility.Parse(Convert(@"<b>\B{x}</b> y")).OfType<TextSegment>().ToList();

        Assert.Equal([@"\B{x}", " y"], segments.Select(s => s.Text));
        Assert.True(segments[0].Bold);
        Assert.False(segments[1].Bold);
    }

    [Fact]
    public void Backslash_before_a_dollar_stays_a_backslash_and_a_dollar()
    {
        Assert.Equal(@"\$", Rendered(@"\$"));
        Assert.Equal(@"a\$b", Rendered(@"a\$b"));
    }

    [Fact]
    public void Backslash_before_formatting_stays_literal()
    {
        var segments = LatexUtility.Parse(Convert(@"x\<b>y</b>")).OfType<TextSegment>().ToList();

        Assert.Equal([@"x\", "y"], segments.Select(s => s.Text));
        Assert.True(segments[1].Bold);
    }

    [Fact]
    public void Maths_is_not_escaped()
    {
        Assert.Equal(@"$\B{x} \{ \$ \\$", Convert(@"\(\B{x} \{ \$ \\\)"));
    }

    [Fact]
    public void Cloze_markup_is_not_escaped_but_text_inside_is()
    {
        Assert.Equal(@"\C1{a \\ b} \\ c", Convert(@"{{c1::a \ b}} \ c"));
    }

    // --- braces ---

    [Fact]
    public void Unbalanced_brace_inside_formatting_does_not_swallow_the_close()
    {
        var segments = LatexUtility.Parse(Convert("<b>{x</b> y")).OfType<TextSegment>().ToList();

        Assert.Equal(["{x", " y"], segments.Select(s => s.Text));
        Assert.True(segments[0].Bold);
        Assert.False(segments[1].Bold);
    }

    [Theory]
    [InlineData("<i>x}</i> y")]
    [InlineData("<u>}{</u> y")]
    [InlineData("<b>a {</b><i>} b</i> y")]
    public void Stray_braces_never_leak_formatting(string html)
    {
        var segments = LatexUtility.Parse(Convert(html)).OfType<TextSegment>().ToList();

        Assert.False(segments[^1].Bold || segments[^1].Italic || segments[^1].Underline);
        Assert.EndsWith(" y", segments[^1].Text);
    }

    [Fact]
    public void Balanced_braces_stay_unescaped()
    {
        Assert.Equal("the set {1, 2}", Convert("the set {1, 2}"));
        Assert.Equal(@"\B{the set {1, 2}} y", Convert("<b>the set {1, 2}</b> y"));
    }

    [Fact]
    public void Braces_in_a_cloze_do_not_break_it()
    {
        var text = Convert("{{c1::a { b}} c");

        Assert.True(ClozeUtility.HasBlanks(text));
        Assert.Equal("[...] c", ClozeUtility.Mask(text, "1"));
        Assert.Equal("a { b", ClozeUtility.Answer(text, "1"));
    }

    // --- speed ---

    [Theory]
    [InlineData("<a ")]
    [InlineData("<")]
    [InlineData("<b")]
    [InlineData("<!--")]
    [InlineData(@"\(")]
    [InlineData("[latex]")]
    [InlineData("{{c1::")]
    public void Pathological_fields_convert_quickly(string unit)
    {
        var field = string.Concat(Enumerable.Repeat(unit, 60_000 / unit.Length));
        var clock = Stopwatch.StartNew();

        _ = Convert(field);
        _ = AnkiTextConverter.ToPlainText(field);

        Assert.True(clock.ElapsedMilliseconds < 500, $"\"{unit}\" took {clock.ElapsedMilliseconds} ms");
    }
}
