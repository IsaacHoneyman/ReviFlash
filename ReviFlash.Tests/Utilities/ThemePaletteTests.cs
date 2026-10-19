using Avalonia.Media;
using ReviFlash.Models;
using ReviFlash.Utilities;
using static ReviFlash.Utilities.ThemePalette;

namespace ReviFlash.Tests.Utilities;

public class ThemePaletteTests
{
    private static string RandomHex(Random random) => $"#{random.Next(0x1000000):X6}";

    public static IEnumerable<object[]> RandomThemes()
    {
        var random = new Random(1234);
        for (int i = 0; i < 2000; i++) yield return [RandomHex(random), RandomHex(random), RandomHex(random)];
    }

    [Fact]
    public void Every_theme_colour_is_derived()
    {
        var colours = Derive(new CustomTheme());

        Assert.Equal(Keys.Select(k => k.Key).Order(), colours.Keys.Order());
    }

    [Fact]
    public void Any_three_colours_give_a_readable_theme()
    {
        foreach (var picks in RandomThemes())
        {
            var theme = new CustomTheme { Background = (string)picks[0], Accent = (string)picks[1], SecondAccent = (string)picks[2] };
            var c = Derive(theme);
            var card = c["CardBackground"];
            string label = string.Join(" ", picks);

            Assert.True(Contrast(c["TextPrimary"], card) >= 7, $"text {label}");
            Assert.True(Contrast(c["TextPrimary"], c["AppBackground"]) >= 7, $"text on background {label}");
            Assert.True(Contrast(c["TextSecondary"], card) >= 4.5, $"secondary {label}");
            Assert.True(Contrast(c["PrimaryButtonForeground"], c["GradientStart"]) >= 4.5, $"buttons {label}");
            foreach (var kind in new[] { "Group", "Folder", "Note" })
            {
                Assert.True(Contrast(c[$"{kind}BadgeForeground"], c[$"{kind}CardBorder"]) >= 4.5, $"{kind} badge {label}");
                Assert.True(Contrast(c[$"{kind}CardBorder"], card) >= 3, $"{kind} border {label}");
            }
        }
    }

    [Fact]
    public void A_mid_tone_background_is_moved_until_text_can_read_on_it()
    {
        var colours = Derive(new CustomTheme { Background = "#6A6A6A" });

        Assert.NotEqual(Color.Parse("#6A6A6A"), colours["AppBackground"]);
        Assert.True(Contrast(colours["TextPrimary"], colours["AppBackground"]) >= 7);
    }

    [Fact]
    public void A_dark_background_the_user_picked_is_kept()
    {
        Assert.Equal(Color.Parse("#0B0E12"), Derive(new CustomTheme { Background = "#0B0E12" })["AppBackground"]);
    }

    [Fact]
    public void An_override_replaces_just_that_colour()
    {
        var plain = Derive(new CustomTheme());
        var theme = new CustomTheme { Overrides = { ["FolderCardBorder"] = "#123456", ["NotAKey"] = "#FFFFFF", ["NoteCardBorder"] = "nonsense" } };

        var colours = Derive(theme);

        Assert.Equal(Color.Parse("#123456"), colours["FolderCardBorder"]);
        Assert.Equal(plain["NoteCardBorder"], colours["NoteCardBorder"]);
        Assert.False(colours.ContainsKey("NotAKey"));
    }

    [Fact]
    public void Bad_picks_fall_back_instead_of_failing()
    {
        var colours = Derive(new CustomTheme { Background = "", Accent = "blue-ish", SecondAccent = "#GG0000" });

        Assert.Equal(Keys.Count, colours.Count);
    }

    [Fact]
    public void A_copy_does_not_share_its_overrides()
    {
        var theme = new CustomTheme { Overrides = { ["TextPrimary"] = "#FFFFFF" } };
        var copy = theme.Copy();

        copy.Overrides["TextPrimary"] = "#000000";

        Assert.Equal("#FFFFFF", theme.Overrides["TextPrimary"]);
    }

    [Theory]
    [InlineData("#0B3A30", "#E040E0")]
    [InlineData("#1A1528", "#3B6FE0")]
    [InlineData("#06201B", "#8A2B20")]
    public void The_swirl_is_the_accent_at_full_strength_whatever_the_background(string background, string accent)
    {
        var colours = Derive(new CustomTheme { Background = background, Accent = accent });
        var picked = new HslColor(Color.Parse(accent));

        foreach (var key in new[] { "SwirlStart", "SwirlMiddle", "SwirlEnd" })
        {
            var swirl = new HslColor(colours[key]);
            double gap = Math.Abs(swirl.H - picked.H) % 360;
            Assert.True(Math.Min(gap, 360 - gap) <= 26, $"{key} hue");
            Assert.True(swirl.S >= 0.55 && swirl.L >= 0.44, $"{key} is muddy");
        }
    }

    [Fact]
    public void Panels_are_calmer_than_a_saturated_background()
    {
        var colours = Derive(new CustomTheme { Background = "#00402A" });

        Assert.True(new HslColor(colours["CardBackground"]).S < new HslColor(colours["AppBackground"]).S - 0.2);
    }

    [Theory]
    [InlineData("#4A4A3F")]
    [InlineData("#3C6E47")]
    [InlineData("#20203A")]
    public void Dark_backgrounds_end_up_as_deep_as_the_built_in_themes(string picked)
    {
        var colours = Derive(new CustomTheme { Background = picked });

        Assert.True(Luminance(colours["AppBackground"]) <= 0.012);
        Assert.True(Luminance(colours["CardBackground"]) < 0.05);
    }
}
