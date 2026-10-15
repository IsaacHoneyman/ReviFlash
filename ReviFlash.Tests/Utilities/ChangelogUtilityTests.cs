using System.Text.Json;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class ChangelogUtilityTests
{
    [Fact]
    public void Each_version_marker_starts_its_own_notes()
    {
        var notes = ChangelogUtility.Parse("ignored\r\n=== 1.4.0\r\n\r\n- New thing\r\n- Another\r\n\r\n=== 1.3.3\r\n- Old thing\r\n");

        Assert.Equal(
            [new ReleaseNotes("1.4.0", "- New thing\n- Another"), new ReleaseNotes("1.3.3", "- Old thing")],
            notes);
    }

    [Fact]
    public void The_app_ships_notes_for_its_own_version()
    {
        Assert.True(ChangelogUtility.HasNotesFor(TextUtility.VersionNumber),
            $"Assets/Changelog.txt has no \"=== {TextUtility.VersionNumber}\" section.");
    }

    [Fact]
    public void Shipped_notes_are_newest_first_and_never_empty()
    {
        var notes = ChangelogUtility.Bundled;

        var versions = notes.Select(n => Version.Parse(n.Version)).ToList();
        Assert.Equal(versions.OrderDescending(), versions);
        Assert.All(notes, n => Assert.False(string.IsNullOrWhiteSpace(n.Body), n.Version));
    }

    [Fact]
    public void Shipped_notes_have_no_stray_maths()
    {
        foreach (var notes in ChangelogUtility.Bundled)
            Assert.DoesNotContain(LatexUtility.Parse(notes.Body), segment => segment is MathSegment);
    }

    [Theory]
    [InlineData("Version 1.3.3", "Version 1.4.0", true, true, true)]
    [InlineData("", "Version 1.4.0", true, true, true)]
    [InlineData("Version 1.4.0", "Version 1.4.0", true, true, false)]
    [InlineData("Version 1.3.3", "Version 1.4.0", false, true, false)]
    [InlineData("Version 1.3.3", "Version 1.4.0", true, false, false)]
    public void Whats_new_opens_once_after_an_update(string previous, string current, bool enabled, bool hasNotes, bool expected)
    {
        Assert.Equal(expected, ChangelogUtility.ShouldShowAfterUpdate(previous, current, enabled, hasNotes));
    }

    [Fact]
    public void Settings_saved_before_the_option_existed_show_whats_new()
    {
        var settings = JsonSerializer.Deserialize<AppMetaData>("""{ "Version": "Version 1.3.3" }""");

        Assert.True(settings!.ShowWhatsNewAfterUpdate);
    }
}
