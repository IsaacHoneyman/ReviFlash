using System.Globalization;
using ReviFlash.Data.Local;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class StatsDateTests
{
    private static string Day(int daysAgo) => DateTime.Now.AddDays(-daysAgo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void AddDeckWithStatsOn(params int[] daysAgo)
    {
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Deck');");
        foreach (var offset in daysAgo)
            Db.Execute("INSERT INTO DeckStats (DeckId, CorrectCount, TotalAttempts, TimeTakenSeconds, DateChecked) VALUES (1, 1, 1, 1, $day);", ("$day", Day(offset)));
    }

    [Fact]
    public void Deck_stats_are_saved_under_the_local_date()
    {
        using var database = new TestDatabase();
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Deck');");

        FlashCardRepository.UpdateDeckStats([(1UL, 1, 2, 3)]);
        FlashCardRepository.UpdateDeckStats([(1UL, 1, 2, 3)]);

        Assert.Equal(Day(0), Db.Scalar<string>("SELECT DateChecked FROM DeckStats;"));
        Assert.Equal(1L, Db.Scalar<long>("SELECT COUNT(*) FROM DeckStats;"));
    }

    [Fact]
    public void Note_study_time_is_saved_under_the_local_date()
    {
        using var database = new TestDatabase();
        var note = NoteRepository.CreateNote("Note", null);

        NoteRepository.AddStudyTime(note.ID, 30);

        Assert.Equal(Day(0), Db.Scalar<string>("SELECT DateStudied FROM NoteStats;"));
    }

    [Theory]
    [InlineData("-1 days", new[] { 0 })]
    [InlineData("-3 days", new[] { 0, 1, 2 })]
    [InlineData("-7 days", new[] { 0, 1, 2, 6 })]
    [InlineData("-14 days", new[] { 0, 1, 2, 6, 7, 8 })]
    public void Periods_cover_that_many_calendar_days_including_today(string modifier, int[] expectedDaysAgo)
    {
        using var database = new TestDatabase();
        AddDeckWithStatsOn(0, 1, 2, 6, 7, 8);

        var days = FlashCardRepository.GetStatsByDate(null, modifier).Select(r => r.date).ToList();

        Assert.Equal(expectedDaysAgo.Select(d => DateOnly.ParseExact(Day(d), "yyyy-MM-dd")).Order(), days);
    }

    [Fact]
    public void Last_month_starts_the_day_after_the_same_date_last_month()
    {
        using var database = new TestDatabase();
        var sameDateLastMonth = DateTime.Now.AddMonths(-1).Date;
        var daysBack = (DateTime.Now.Date - sameDateLastMonth).Days;
        AddDeckWithStatsOn(daysBack - 1, daysBack, daysBack + 1);

        var days = FlashCardRepository.GetStatsByDate(null, "-1 months").Select(r => r.date).ToList();

        Assert.Equal([DateOnly.ParseExact(Day(daysBack - 1), "yyyy-MM-dd")], days);
    }

    [Fact]
    public void Note_periods_use_the_same_calendar_days()
    {
        using var database = new TestDatabase();
        var note = NoteRepository.CreateNote("Note", null);
        foreach (var offset in new[] { 0, 6, 7 })
            Db.Execute("INSERT INTO NoteStats (NoteID, DateStudied, Seconds) VALUES ($id, $day, 10);", ("$id", note.ID), ("$day", Day(offset)));

        Assert.Equal(10, NoteRepository.GetStudySeconds(null, "-1 days"));
        Assert.Equal(20, NoteRepository.GetStudySeconds(null, "-7 days"));
        Assert.Equal(30, NoteRepository.GetStudySeconds(null, "-14 days"));
    }
}
