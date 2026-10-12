using ReviFlash.Data.Local;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class DatabaseSmokeTests
{
    [Fact]
    public void A_new_database_has_the_library_tables()
    {
        using var database = new TestDatabase();

        foreach (var table in new[] { "Folders", "Decks", "Cards", "DeckStats", "Notes", "NoteStats" })
            Assert.Equal(1L, Db.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;", ("$name", table)));
    }
}
