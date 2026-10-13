using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class TextUtilityTests
{
    // Two lines of ten characters, wrapping at spaces like a TextBlock.
    private static bool FitsTwoLinesOfTen(string text)
    {
        int lines = 1, width = 0;
        foreach (var word in text.Split(' '))
        {
            int needed = width == 0 ? word.Length : width + 1 + word.Length;
            if (needed <= 10) { width = needed; continue; }
            lines++;
            width = word.Length;
        }
        return lines <= 2;
    }

    [Fact]
    public void Text_that_fits_is_left_alone()
    {
        Assert.Equal("Short", TextUtility.TrimToFit("Short", FitsTwoLinesOfTen));
    }

    [Fact]
    public void A_title_cut_at_a_word_break_still_ends_with_an_ellipsis()
    {
        var trimmed = TextUtility.TrimToFit("Intro To Computer Architecture", FitsTwoLinesOfTen);

        Assert.EndsWith(TextUtility.Ellipsis, trimmed);
        Assert.StartsWith("Intro To", trimmed);
        Assert.True(FitsTwoLinesOfTen(trimmed));
    }

    [Fact]
    public void Trimming_keeps_as_much_of_the_title_as_fits()
    {
        var trimmed = TextUtility.TrimToFit("Intro To Computer Architecture", FitsTwoLinesOfTen);

        Assert.Equal("Intro To Computer…", trimmed);
    }

    [Fact]
    public void Trimming_drops_the_space_before_the_ellipsis()
    {
        var trimmed = TextUtility.TrimToFit("aaaa bbbb cccc dddd", text => text.Length <= 10);

        Assert.Equal("aaaa bbbb…", trimmed);
    }

    [Fact]
    public void Nothing_fitting_leaves_just_the_ellipsis()
    {
        Assert.Equal(TextUtility.Ellipsis, TextUtility.TrimToFit("Anything", text => text == TextUtility.Ellipsis));
    }
}
