using System.IO.Compression;
using Microsoft.Data.Sqlite;
using ReviFlash.Data.Local;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class AnkiImporterTests
{
    private const string Models = """{"1":{"name":"Basic","type":0,"flds":[{"name":"Front","ord":0},{"name":"Back","ord":1}],"tmpls":[{"ord":0,"qfmt":"{{Front}}","afmt":"{{Back}}"}]}}""";
    private const string Decks = """{"1":{"name":"Default"},"2":{"name":"Home"},"3":{"name":"Filtered"}}""";

    private static string BuildPackage(params (long NoteId, long Did, long Odid, string Front)[] cards)
    {
        var directory = Path.Combine(TestEnvironment.DataRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var collection = Path.Combine(directory, "collection.anki2");

        using (var anki = new SqliteConnection($"Data Source={collection};Pooling=False"))
        {
            anki.Open();
            Execute(anki, "CREATE TABLE col (models TEXT, decks TEXT)");
            Execute(anki, "CREATE TABLE notes (id INTEGER, mid INTEGER, flds TEXT)");
            Execute(anki, "CREATE TABLE cards (id INTEGER, nid INTEGER, did INTEGER, odid INTEGER, ord INTEGER)");
            using (var insert = anki.CreateCommand())
            {
                insert.CommandText = "INSERT INTO col VALUES ($models, $decks)";
                insert.Parameters.AddWithValue("$models", Models);
                insert.Parameters.AddWithValue("$decks", Decks);
                insert.ExecuteNonQuery();
            }

            foreach (var (noteId, did, odid, front) in cards)
            {
                Execute(anki, $"INSERT INTO notes VALUES ({noteId}, 1, '{front}\u001fback')");
                Execute(anki, $"INSERT INTO cards VALUES ({noteId}, {noteId}, {did}, {odid}, 0)");
            }
        }

        SqliteConnection.ClearAllPools();
        var package = Path.Combine(directory, "deck.apkg");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create)) zip.CreateEntryFromFile(collection, "collection.anki2");
        return package;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, List<string>> CardsByDeck() =>
        Db.Query("SELECT d.Name, c.Front FROM Cards c JOIN Decks d ON d.ID = c.DeckID ORDER BY c.ID", r => (r.GetString(0), r.GetString(1)))
            .GroupBy(row => row.Item1).ToDictionary(group => group.Key, group => group.Select(row => row.Item2).ToList());

    [Fact]
    public void Cards_import_into_their_decks()
    {
        using var database = new TestDatabase();

        AnkiImporter.Import(BuildPackage((10, 1, 0, "one"), (11, 2, 0, "two")));

        var decks = CardsByDeck();
        Assert.Equal(["one"], decks["Default"]);
        Assert.Equal(["two"], decks["Home"]);
    }

    [Fact]
    public void Cards_in_a_filtered_deck_import_into_their_home_deck()
    {
        using var database = new TestDatabase();

        var result = AnkiImporter.Import(BuildPackage((10, 3, 2, "filtered"), (11, 2, 0, "home")));

        var decks = CardsByDeck();
        Assert.False(decks.ContainsKey("Filtered"));
        Assert.Equal(["filtered", "home"], decks["Home"]);
        Assert.Equal(1, result.Decks);
    }
}
