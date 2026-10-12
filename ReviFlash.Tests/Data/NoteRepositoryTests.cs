using System.Globalization;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class NoteRepositoryTests
{
    [Fact]
    public void GetAllNotes_loads_the_last_study_day_for_studied_notes_only()
    {
        using var database = new TestDatabase();
        var studied = NoteRepository.CreateNote("Studied", null);
        var untouched = NoteRepository.CreateNote("Untouched", null);
        Db.Execute("INSERT INTO NoteStats (NoteID, DateStudied, Seconds) VALUES ($id, '2026-01-02', 5), ($id, '2026-03-04', 5);", ("$id", studied.ID));

        var notes = NoteRepository.GetAllNotes();

        Assert.Equal(new DateTime(2026, 3, 4), notes.Single(n => n.ID == studied.ID).LastStudied);
        Assert.Null(notes.Single(n => n.ID == untouched.ID).LastStudied);
    }

    [Fact]
    public void A_note_studied_today_ties_a_set_studied_today_and_beats_a_note_only_edited_earlier()
    {
        using var database = new TestDatabase();
        var studied = NoteRepository.CreateNote("Studied", null);
        NoteRepository.AddStudyTime(studied.ID, 60);
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Deck');");
        FlashCardRepository.UpdateDeckStats([(1UL, 1, 1, 1)]);

        var note = NoteRepository.GetAllNotes().Single();
        var deck = FlashCardRepository.GetAllDecks().Single();

        Assert.Equal(DateTime.Today, note.SortLastActivity);
        Assert.Equal(deck.SortLastActivity, note.SortLastActivity);
    }

    [Fact]
    public void Folder_roll_up_ranks_a_studied_note_by_its_study_day()
    {
        using var database = new TestDatabase();
        var note = NoteRepository.CreateNote("Studied", null);
        Db.Execute("INSERT INTO NoteStats (NoteID, DateStudied, Seconds) VALUES ($id, '2020-01-01', 5);", ("$id", note.ID));

        var loaded = NoteRepository.GetAllNotes().Single();

        Assert.Equal(new DateTime(2020, 1, 1), loaded.SortLastActivity);
    }
}
