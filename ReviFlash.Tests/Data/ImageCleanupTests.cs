using System.IO.Compression;
using ReviFlash.Data.Backup.Local;
using ReviFlash.Data.Local;
using ReviFlash.Utilities;
using SkiaSharp;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class ImageCleanupTests
{
    private static int _colour;

    /// <summary> A new image each call, written an hour ago so a cleanup started now may remove it. </summary>
    private static string OldImage()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(new SKColor((uint)(0xFF000000 | (uint)Interlocked.Increment(ref _colour) * 7919)));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var name = ImageStore.Add(data.ToArray());
        File.SetLastWriteTimeUtc(ImageStore.PathOf(name)!, DateTime.UtcNow.AddHours(-1));
        return name;
    }

    private static bool Stored(string name) => File.Exists(ImageStore.PathOf(name));

    [Fact]
    public void Startup_cleanup_keeps_images_anything_shows_and_removes_the_rest()
    {
        using var database = new TestDatabase();
        var inCard = OldImage();
        var inOption = OldImage();
        var inPair = OldImage();
        var inNote = OldImage();
        var unused = OldImage();

        Db.Execute("INSERT INTO Decks (Name) VALUES ('Set');");
        Db.Execute("INSERT INTO Cards (DeckID, CardType, Front, Back) VALUES (1, 'FlipFlashCard', $front, 'Back');", ("$front", "Q " + ImageToken.Format(inCard, 50)));
        Db.Execute("INSERT INTO CardOptions (CardID, OptionIndex, OptionText, IsCorrect) VALUES (1, 0, $text, 1);", ("$text", ImageToken.Format(inOption)));
        Db.Execute("INSERT INTO MatchCardPairs (CardID, PairIndex, LeftText, RightText) VALUES (1, 0, 'L', $text);", ("$text", ImageToken.Format(inPair)));
        Db.Execute("INSERT INTO Notes (SyncID, Name, Content, CreatedAt, UpdatedAt) VALUES ('n', 'Note', $text, '2026-01-01 00:00:00', '2026-01-01 00:00:00');", ("$text", "Para\n\n" + ImageToken.Format(inNote)));

        ImageStore.RemoveUnused(DateTime.Now);

        Assert.True(Stored(inCard));
        Assert.True(Stored(inOption));
        Assert.True(Stored(inPair));
        Assert.True(Stored(inNote));
        Assert.False(Stored(unused));
    }

    [Fact]
    public void An_image_added_while_the_cleanup_runs_is_kept_even_if_it_was_stored_before()
    {
        using var database = new TestDatabase();
        var name = OldImage();
        var startedAt = DateTime.Now;

        ImageStore.Add(File.ReadAllBytes(ImageStore.PathOf(name)!));
        ImageStore.RemoveUnused(startedAt.AddSeconds(-1));

        Assert.True(Stored(name));
    }

    [Fact]
    public void A_backup_brings_its_images_back()
    {
        using var database = new TestDatabase();
        var name = OldImage();
        Db.Execute("INSERT INTO Decks (Name) VALUES ('Set');");
        Db.Execute("INSERT INTO Cards (DeckID, CardType, Front, Back) VALUES (1, 'FlipFlashCard', $front, 'Back');", ("$front", ImageToken.Format(name)));

        var folder = Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");
        BackupManager.TryCreateBackup(folder);
        var zip = Directory.GetFiles(folder, "*.zip").Single();
        File.Delete(ImageStore.PathOf(name)!);

        BackupManager.TryRestoreBackup(zip);
        MetaDataManager.Data.DatabasePath = database.Path;

        Assert.True(Stored(name));
    }

    [Fact]
    public void A_restore_ignores_entries_that_arent_stored_images()
    {
        using var database = new TestDatabase();
        var folder = Path.Combine(TestEnvironment.DataRoot, $"backups-{Guid.NewGuid():N}");
        BackupManager.TryCreateBackup(folder);
        var zip = Directory.GetFiles(folder, "*.zip").Single();
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            foreach (var bad in new[] { "images/../escaped.webp", "images/notes.txt", "images/ABCDEF0123456789ABCDEF0123456789.webp" })
            {
                using var writer = new StreamWriter(archive.CreateEntry(bad).Open());
                writer.Write("not an image");
            }
        }

        BackupManager.TryRestoreBackup(zip);
        MetaDataManager.Data.DatabasePath = database.Path;

        Assert.False(File.Exists(Path.Combine(TextUtility.BaseDirectory, "escaped.webp")));
        Assert.False(File.Exists(Path.Combine(ImageStore.Folder, "notes.txt")));
        Assert.DoesNotContain(ImageStore.StoredNames(), name => name.StartsWith("ABCDEF", StringComparison.Ordinal));
    }
}
