using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class NoteTextTests
{
    [Fact]
    public void SplitBlocks_splits_on_blank_lines()
    {
        Assert.Equal(["one", "two"], NoteText.SplitBlocks("one\n\ntwo"));
    }

    [Fact]
    public void SplitBlocks_keeps_display_maths_with_a_blank_line_as_one_block()
    {
        var blocks = NoteText.SplitBlocks("Intro\n\n$$\na = b\n\nc = d\n$$\n\nEnd");

        Assert.Equal(["Intro", "$$\na = b\n\nc = d\n$$", "End"], blocks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n\n\n")]
    public void SplitBlocks_never_returns_an_empty_list(string? content)
    {
        Assert.Equal([""], NoteText.SplitBlocks(content));
    }

    [Fact]
    public void JoinBlocks_puts_one_blank_line_between_blocks_and_drops_empty_ones()
    {
        Assert.Equal("one\n\ntwo", NoteText.JoinBlocks(["one\n", "", "  ", "two"]));
    }
}
