using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class NoteTextSplitTests
{
    [Fact]
    public void SplitBlocks_treats_adjacent_inline_formulas_as_inline_maths()
    {
        Assert.Equal(["$a$$b$", "para2"], NoteText.SplitBlocks("$a$$b$\n\npara2"));
    }

    [Fact]
    public void SplitBlocks_ignores_escaped_dollars()
    {
        Assert.Equal(["costs \\$\\$5", "next"], NoteText.SplitBlocks("costs \\$\\$5\n\nnext"));
    }

    [Fact]
    public void SplitBlocks_splits_after_an_unclosed_display_delimiter()
    {
        Assert.Equal(["$$ x", "para"], NoteText.SplitBlocks("$$ x\n\npara"));
    }

    [Fact]
    public void SplitBlocks_keeps_a_closed_display_block_with_a_blank_line_as_one_block()
    {
        Assert.Equal(["$$ a\n\nb $$", "after"], NoteText.SplitBlocks("$$ a\n\nb $$\n\nafter"));
    }

    [Fact]
    public void SplitBlocks_keeps_display_maths_that_closes_in_a_later_paragraph()
    {
        Assert.Equal(["before $$ a\n\nb $$ after", "end"], NoteText.SplitBlocks("before $$ a\n\nb $$ after\n\nend"));
    }

    [Fact]
    public void SplitBlocks_does_not_let_an_unclosed_display_swallow_later_paragraphs()
    {
        Assert.Equal(["a $$ b", "c", "d"], NoteText.SplitBlocks("a $$ b\n\nc\n\nd"));
    }

    [Theory]
    [InlineData("$$ x", true)]
    [InlineData("$$ x $$", false)]
    [InlineData("$a$$b$", false)]
    [InlineData("costs \\$\\$5", false)]
    [InlineData("$$\na\n\nb", true)]
    [InlineData("plain", false)]
    public void EndsInDisplayMath_matches_how_the_maths_is_read(string text, bool expected)
    {
        Assert.Equal(expected, NoteText.EndsInDisplayMath(text));
    }

    [Fact]
    public void JoinBlocks_treats_a_null_block_as_empty()
    {
        Assert.Equal("one\n\ntwo", NoteText.JoinBlocks(["one", null, "two"]));
    }

    [Fact]
    public void SplitBlocks_closes_display_maths_after_an_escaped_backslash()
    {
        Assert.Equal(["$$\na \\\\\n\nb \\\\$$", "after"], NoteText.SplitBlocks("$$\na \\\\\n\nb \\\\$$\n\nafter"));
    }
}
