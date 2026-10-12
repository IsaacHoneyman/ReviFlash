using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class TextFormattingTests
{
    private static string Bullets(string text, int start, int end) => TextFormatting.ToggleBullets(text, start, end).Text;

    private static string Wrap(string text, int start, int end, string open, string close) =>
        TextFormatting.Wrap(text, start, end, open, close).Text;

    [Fact]
    public void Bullets_after_select_line_cover_only_that_line()
    {
        var line = TextFormatting.SelectLineRange("a\nb\nc", 0, 0);
        Assert.Equal((0, 2), (line.SelectionStart, line.SelectionEnd));

        Assert.Equal("- a\nb\nc", Bullets("a\nb\nc", line.SelectionStart, line.SelectionEnd));
    }

    [Fact]
    public void Bullets_are_removed_when_every_line_has_one_including_the_trailing_newline()
    {
        Assert.Equal("a\nb\n", Bullets("- a\n- b\n", 0, 8));
    }

    [Fact]
    public void Bullets_are_added_only_to_lines_without_one()
    {
        Assert.Equal("- a\n- b", Bullets("- a\nb", 0, 5));
    }

    [Fact]
    public void Bullets_leave_blank_lines_alone_in_both_directions()
    {
        Assert.Equal("a\n\nb", Bullets("- a\n\n- b", 0, 8));
        Assert.Equal("- a\n\n- b", Bullets("a\n\nb", 0, 4));
    }

    [Fact]
    public void Bullets_keep_crlf_and_indent()
    {
        Assert.Equal("- a\r\n  - b\r\n", Bullets("a\r\n  b\r\n", 0, 9));
        Assert.Equal("a\r\n  b", Bullets("- a\r\n  - b", 0, 10));
    }

    [Fact]
    public void Bullets_treat_an_escaped_dash_as_text()
    {
        Assert.Equal(@"- \- a", Bullets(@"\- a", 0, 0));
    }

    [Fact]
    public void Bullets_work_on_the_last_line_without_a_newline()
    {
        Assert.Equal("a\n- b", Bullets("a\nb", 3, 3));
    }

    [Fact]
    public void Bullets_move_the_caret_with_the_text()
    {
        var edit = TextFormatting.ToggleBullets("abc", 2, 2);
        Assert.Equal(("- abc", 4, 4), (edit.Text, edit.SelectionStart, edit.SelectionEnd));
    }

    [Fact]
    public void Bullets_on_a_selection_ending_at_a_line_start_exclude_that_line()
    {
        Assert.Equal("- a\nb\n", Bullets("a\nb\n", 0, 2));
    }

    [Fact]
    public void Select_line_extends_to_the_next_line_when_pressed_again()
    {
        var edit = TextFormatting.SelectLineRange("a\nb\nc", 0, 2);
        Assert.Equal((0, 4), (edit.SelectionStart, edit.SelectionEnd));
    }

    [Theory]
    [InlineData(@"\B{", "}")]
    [InlineData(@"\H1{", "}")]
    public void Wrap_wraps_and_unwraps_outside_markers(string open, string close)
    {
        var wrapped = TextFormatting.Wrap("x", 0, 1, open, close);
        Assert.Equal((open + "x" + close, open.Length, open.Length + 1), (wrapped.Text, wrapped.SelectionStart, wrapped.SelectionEnd));

        var unwrapped = TextFormatting.Wrap(wrapped.Text, wrapped.SelectionStart, wrapped.SelectionEnd, open, close);
        Assert.Equal(("x", 0, 1), (unwrapped.Text, unwrapped.SelectionStart, unwrapped.SelectionEnd));
    }

    [Fact]
    public void Wrap_with_no_selection_inserts_an_empty_pair_with_the_caret_inside()
    {
        var edit = TextFormatting.Wrap("ab", 1, 1, @"\B{", "}");
        Assert.Equal((@"a\B{}b", 4, 4), (edit.Text, edit.SelectionStart, edit.SelectionEnd));
    }

    [Fact]
    public void Wrap_unwraps_when_the_selection_includes_the_markers()
    {
        var edit = TextFormatting.Wrap(@"\B{x}", 0, 5, @"\B{", "}");
        Assert.Equal(("x", 0, 1), (edit.Text, edit.SelectionStart, edit.SelectionEnd));
    }

    [Fact]
    public void Wrap_does_not_unwrap_two_separate_groups()
    {
        Assert.Equal(@"\B{\B{a}b\B{c}}", Wrap(@"\B{a}b\B{c}", 0, 11, @"\B{", "}"));
    }

    [Fact]
    public void Wrap_keeps_trailing_line_breaks_outside_the_pair()
    {
        Assert.Equal("\\H1{title}\n", Wrap("title\n", 0, 6, @"\H1{", "}"));
        Assert.Equal("\\B{a}\r\n", Wrap("a\r\n", 0, 3, @"\B{", "}"));
    }

    [Fact]
    public void Wrap_unwraps_a_whole_selected_line_with_its_line_break()
    {
        Assert.Equal("x\n", Wrap("\\B{x}\n", 0, 6, @"\B{", "}"));
    }

    [Fact]
    public void Inline_maths_unwraps_a_single_dollar_pair()
    {
        Assert.Equal("x", Wrap("$x$", 1, 2, "$", "$"));
        Assert.Equal("x", Wrap("$x$", 0, 3, "$", "$"));
        Assert.Equal("$x$", Wrap("x", 0, 1, "$", "$"));
    }

    [Fact]
    public void Inline_maths_does_not_turn_display_maths_inline()
    {
        Assert.Equal("$$$x$$$", Wrap("$$x$$", 2, 3, "$", "$"));
    }

    [Fact]
    public void Inline_maths_does_not_merge_adjacent_formulas()
    {
        Assert.Equal("$a$$$$b$", Wrap("$a$$b$", 3, 3, "$", "$"));
    }

    [Fact]
    public void Inline_maths_does_not_eat_an_escaped_dollar()
    {
        Assert.Equal(@"\$$x$$", Wrap(@"\$x$", 2, 3, "$", "$"));
    }

    [Fact]
    public void Display_maths_unwraps_both_ways()
    {
        Assert.Equal("x", Wrap("$$x$$", 2, 3, "$$", "$$"));
        Assert.Equal("x", Wrap("$$x$$", 0, 5, "$$", "$$"));
    }

    [Fact]
    public void Empty_dollar_pair_toggles_off()
    {
        Assert.Equal("", Wrap("$$", 1, 1, "$", "$"));
    }
}
