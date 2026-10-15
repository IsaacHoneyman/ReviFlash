using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using ReviFlash.Data.Local;
using SkiaSharp;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class AnkiImporterTests
{
    private const string Models = """{"1":{"name":"Basic","type":0,"flds":[{"name":"Front","ord":0},{"name":"Back","ord":1}],"tmpls":[{"ord":0,"qfmt":"{{Front}}","afmt":"{{Back}}"}]}}""";
    private const string Decks = """{"1":{"name":"Default"},"2":{"name":"Home"},"3":{"name":"Filtered"}}""";

    private static string BuildPackage(params (long NoteId, long Did, long Odid, string Front)[] cards) => BuildPackage(null, false, cards);

    /// <param name="media"> Files by name; <paramref name="newFormat"/> writes them the way Anki 2.1.50+ does (zstd, protobuf list). </param>
    private static string BuildPackage(Dictionary<string, byte[]>? media, bool newFormat, params (long NoteId, long Did, long Odid, string Front)[] cards)
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
                using (var note = anki.CreateCommand())
                {
                    note.CommandText = $"INSERT INTO notes VALUES ({noteId}, 1, $fields)";
                    note.Parameters.AddWithValue("$fields", front + "\u001fback");
                    note.ExecuteNonQuery();
                }
                Execute(anki, $"INSERT INTO cards VALUES ({noteId}, {noteId}, {did}, {odid}, 0)");
            }
        }

        SqliteConnection.ClearAllPools();
        var package = Path.Combine(directory, "deck.apkg");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(collection, "collection.anki2");
            if (media is not null) AddMedia(zip, media, newFormat);
        }
        return package;
    }

    private static void AddMedia(ZipArchive zip, Dictionary<string, byte[]> media, bool newFormat)
    {
        var files = media.ToList();
        for (var index = 0; index < files.Count; index++)
            Write(zip, index.ToString(), newFormat ? Zstd(files[index].Value) : files[index].Value);

        if (!newFormat)
        {
            Write(zip, "media", Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(
                files.Select((file, index) => (index, file.Key)).ToDictionary(pair => pair.index.ToString(), pair => pair.Key))));
            return;
        }

        // MediaEntries { repeated MediaEntry entries = 1 }, MediaEntry { string name = 1; uint32 size = 2 }.
        var list = new List<byte>();
        foreach (var (name, bytes) in files)
        {
            var entry = new List<byte> { 0x0A };
            var nameBytes = Encoding.UTF8.GetBytes(name);
            entry.AddRange(Varint(nameBytes.Length));
            entry.AddRange(nameBytes);
            entry.Add(0x10);
            entry.AddRange(Varint(bytes.Length));
            list.Add(0x0A);
            list.AddRange(Varint(entry.Count));
            list.AddRange(entry);
        }
        Write(zip, "media", Zstd([.. list]));
    }

    private static IEnumerable<byte> Varint(int value)
    {
        var v = (uint)value;
        while (v >= 0x80) { yield return (byte)(v | 0x80); v >>= 7; }
        yield return (byte)v;
    }

    private static byte[] Zstd(byte[] data)
    {
        using var compressor = new ZstdSharp.Compressor();
        return compressor.Wrap(data).ToArray();
    }

    private static void Write(ZipArchive zip, string name, byte[] data)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(data);
    }

    private static byte[] Png(SKColor colour)
    {
        using var bitmap = new SKBitmap(20, 10);
        bitmap.Erase(colour);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static List<string> Fronts() => Db.Query("SELECT Front FROM Cards ORDER BY ID", r => r.GetString(0));

    private static string StoredImageIn(string front) =>
        Assert.IsType<ReviFlash.Utilities.ImageSegment>(Assert.Single(ReviFlash.Utilities.LatexUtility.Parse(front), segment => segment is ReviFlash.Utilities.ImageSegment)).Name;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Images_come_across_from_old_and_new_packages(bool newFormat)
    {
        using var database = new TestDatabase();
        var media = new Dictionary<string, byte[]> { ["pic.png"] = Png(SKColors.Red), ["my pic & co.png"] = Png(SKColors.Blue) };

        var result = AnkiImporter.Import(BuildPackage(media, newFormat,
            (10, 1, 0, "First <img src=\"pic.png\">"), (11, 1, 0, "<img src=\"my pic &amp; co.png\">"), (12, 1, 0, "Again <img src='pic.png'>")));

        var fronts = Fronts();
        Assert.StartsWith("First\n", fronts[0]);
        Assert.All(fronts, front => Assert.True(File.Exists(ImageStore.PathOf(StoredImageIn(front)))));
        Assert.Equal(StoredImageIn(fronts[0]), StoredImageIn(fronts[2]));
        Assert.Equal(2, result.Images);
        Assert.Equal(0, result.CardsWithMissingImages);
    }

    [Fact]
    public void An_image_missing_from_the_package_stays_marked()
    {
        using var database = new TestDatabase();

        var result = AnkiImporter.Import(BuildPackage(new Dictionary<string, byte[]> { ["here.png"] = Png(SKColors.Green), ["broken.png"] = [1, 2, 3] }, false,
            (10, 1, 0, "Gone <img src=\"gone.png\">"), (11, 1, 0, "Broken <img src=\"broken.png\">")));

        Assert.Equal(["Gone [image]", "Broken [image]"], Fronts());
        Assert.Equal(0, result.Images);
        Assert.Equal(2, result.CardsWithMissingImages);
        Assert.Equal(["Default"], result.DecksWithMissingImages);
    }
}
