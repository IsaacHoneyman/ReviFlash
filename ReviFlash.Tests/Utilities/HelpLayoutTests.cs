using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class HelpLayoutTests
{
    [Fact]
    public void A_wide_window_docks_help_at_full_width()
    {
        Assert.Equal((620, true), HelpLayout.Fit(1920));
    }

    [Fact]
    public void A_medium_window_docks_narrower_help_and_leaves_the_page_its_minimum()
    {
        var (width, docked) = HelpLayout.Fit(1300);

        Assert.True(docked);
        Assert.Equal(1300 - HelpLayout.PageMinWidth, width);
    }

    [Theory]
    [InlineData(960)]
    [InlineData(1100)]
    public void A_narrow_window_floats_help_over_the_page_instead_of_squashing_it(double windowWidth)
    {
        Assert.Equal((620, false), HelpLayout.Fit(windowWidth));
    }

    [Fact]
    public void Floating_help_is_never_wider_than_the_window()
    {
        Assert.Equal((500, false), HelpLayout.Fit(500));
    }
}
