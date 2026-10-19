using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class CardSubtitleTests
{
    [Fact]
    public void A_set_says_when_it_was_last_studied()
    {
        Assert.Equal("Not studied yet", new FlashCardDeck("Set", 1, 3).CardSubtitle);
        Assert.Equal("Studied today", new FlashCardDeck("Set", 1, 3, null, 0, DateTime.Today).CardSubtitle);
        Assert.Equal("Studied 3 days ago", new FlashCardDeck("Set", 1, 3, null, 0, DateTime.Today.AddDays(-3)).CardSubtitle);
    }

    [Fact]
    public void A_group_gives_its_cards_and_when_it_was_studied()
    {
        var group = new StudyGroup("Group", 1, 2, 12, null, 0, DateTime.Today.AddDays(-1));

        Assert.Equal("12 cards · studied yesterday", group.CardSubtitle);
    }

    [Fact]
    public void A_search_result_adds_where_it_lives_without_losing_the_summary()
    {
        var folder = new Folder("Folder") { SetCount = 2 };
        var set = new FlashCardDeck("Set", 1, 3);

        folder.FolderPath = "Year 2";
        set.FolderPath = "Year 2 / Maths";

        Assert.Equal("2 sets · in Year 2", folder.CardSubtitle);
        Assert.Equal("Not studied yet · in Year 2 / Maths", set.CardSubtitle);
    }

    [Fact]
    public void The_subtitle_updates_when_a_count_or_study_day_changes()
    {
        var group = new StudyGroup("Group", 1, 1, 1);
        var changed = new List<string?>();
        group.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        group.CardCount = 5;
        group.LastStudied = DateTime.Today;

        Assert.Equal(2, changed.Count(name => name == nameof(LibraryItem.CardSubtitle)));
        Assert.Equal("5 cards · studied today", group.CardSubtitle);
    }

    [Fact]
    public void Study_days_read_relative_to_today_for_a_week()
    {
        Assert.Equal("today", TextUtility.FormatDay(DateTime.Today));
        Assert.Equal("yesterday", TextUtility.FormatDay(DateTime.Today.AddDays(-1)));
        Assert.Equal("6 days ago", TextUtility.FormatDay(DateTime.Today.AddDays(-6)));
        Assert.Equal(DateTime.Today.AddDays(-30).Year == DateTime.Today.Year ? DateTime.Today.AddDays(-30).ToString("d MMM") : DateTime.Today.AddDays(-30).ToString("d MMM yyyy"),
            TextUtility.FormatDay(DateTime.Today.AddDays(-30)));
    }

    [Fact]
    public void A_note_shows_its_words_in_the_badge_and_its_last_edit_below()
    {
        var note = new Note(1, "sync", "Lecture", null, 1240, DateTime.Now, DateTime.Today.AddDays(-1).AddHours(12));

        Assert.Equal(("1.2k", "Words"), (note.WordCountText, note.WordCountLabel));
        Assert.Equal("Edited yesterday", note.CardSubtitle);

        note.WordCount = 1;
        Assert.Equal("Word", note.WordCountLabel);
    }

    [Theory]
    [InlineData(950, "950")]
    [InlineData(1000, "1k")]
    [InlineData(1290, "1.2k")]
    [InlineData(12_400, "12k")]
    public void Badge_counts_stay_short(int count, string expected)
    {
        Assert.Equal(expected, TextUtility.Compact(count));
    }
}
