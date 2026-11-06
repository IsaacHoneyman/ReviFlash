using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class ThemeUpgradeTests
{
    [Fact]
    public void Updating_to_the_new_look_switches_any_theme_to_it()
    {
        Assert.Equal("Ink", ThemeUpgrade.ThemeAfterUpdate("Sakura", "Version 1.4.3", "Version 1.5.0", "Ink"));
    }

    [Theory]
    [InlineData("Version 1.5.0", "Version 1.5.0")]
    [InlineData("Version 1.5.0", "Version 1.5.1")]
    [InlineData("Version 1.4.2", "Version 1.4.3")]
    public void A_theme_picked_since_or_an_update_before_it_is_left_alone(string previous, string current)
    {
        Assert.Equal("Sakura", ThemeUpgrade.ThemeAfterUpdate("Sakura", previous, current, "Ink"));
    }

    [Fact]
    public void A_fresh_install_keeps_its_starting_theme()
    {
        Assert.Equal("Ink", ThemeUpgrade.ThemeAfterUpdate("Ink", "", "Version 1.5.0", "Ink"));
    }
}
