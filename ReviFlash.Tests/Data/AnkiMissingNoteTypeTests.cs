using System.IO.Compression;
using Microsoft.Data.Sqlite;
using ReviFlash.Data.Local;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class AnkiMissingNoteTypeTests
{
    private const string Models = """{"1":{"name":"Basic","type":0,"flds":[{"name":"Front","ord":0},{"name":"Back","ord":1}],"tmpls":[{"ord":0,"qfmt":"{{Front}}","afmt":"{{Back}}"}]}}""";

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string BuildPackage()
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
                insert.Parameters.AddWithValue("$decks", """{"1":{"name":"Default"}}""");
                insert.ExecuteNonQuery();
            }
            Execute(anki, "INSERT INTO notes VALUES (10, 1, 'one' || char(31) || 'back')");
            Execute(anki, "INSERT INTO notes VALUES (11, 99, 'two' || char(31) || 'back')");
            Execute(anki, "INSERT INTO cards VALUES (10, 10, 1, 0, 0)");
            Execute(anki, "INSERT INTO cards VALUES (11, 11, 1, 0, 0)");
        }

        SqliteConnection.ClearAllPools();
        var package = Path.Combine(directory, "deck.apkg");
        using var zip = ZipFile.Open(package, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(collection, "collection.anki2");
        return package;
    }

    [Fact]
    public void A_note_whose_note_type_is_missing_is_counted_as_skipped()
    {
        using var database = new TestDatabase();

        var result = AnkiImporter.Import(BuildPackage());

        Assert.Equal(1, result.Cards);
        Assert.Equal(1, result.Skipped);
    }
}
