using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

/// <summary> Import counts: images come across, except ones missing or unreadable, which stay as "[image]"; Skipped notes are image occlusion, have an unknown note type or have nothing to ask. </summary>
public sealed record AnkiImportResult(int Decks, int Cards, int Images, int CardsWithMissingImages, int Skipped, IReadOnlyList<string> DecksWithMissingImages);

/// <summary> Imports an Anki .apkg/.colpkg as decks (parent decks become folders) of Flip, Type and Cloze cards, without scheduling. </summary>
public static partial class AnkiImporter
{
    private sealed record NoteType(string Name, bool IsCloze, List<string> Fields, List<(string Question, string Answer)> Templates);

    private sealed record Note(long ID, long NoteTypeID, string[] Fields);

    public static AnkiImportResult Import(string packagePath)
    {
        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(packagePath);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("This isn't an Anki deck file. Choose an .apkg exported from Anki.");
        }

        using var _ = zip;
        var collectionPath = ExtractCollection(zip);
        try
        {
            return ImportCollection(collectionPath, new Media(zip));
        }
        finally
        {
            File.Delete(collectionPath);
        }
    }

    // --- Reading the package ---

    /// <summary> Copies the collection out of the zip, newest format first: newer packages also hold a stub "please update Anki" collection.anki2. </summary>
    private static string ExtractCollection(ZipArchive zip)
    {
        var target = Path.Combine(Path.GetTempPath(), $"reviflash-anki-{Guid.NewGuid():N}.db");

        if (zip.GetEntry("collection.anki21b") is { } compressed)
        {
            using var output = File.Create(target);
            output.Write(ReadEntry(compressed));
            return target;
        }

        var legacy = zip.GetEntry("collection.anki21") ?? zip.GetEntry("collection.anki2")
            ?? throw new InvalidDataException("This file doesn't contain an Anki collection. Choose an .apkg exported from Anki.");
        legacy.ExtractToFile(target);
        return target;
    }

    /// <summary> An entry's bytes, unpacked when zstd-compressed as in newer packages. </summary>
    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        using var raw = new MemoryStream();
        input.CopyTo(raw);
        var bytes = raw.ToArray();
        if (bytes is not [0x28, 0xB5, 0x2F, 0xFD, ..]) return bytes;

        using var decompressed = new ZstdSharp.DecompressionStream(new MemoryStream(bytes));
        using var output = new MemoryStream();
        decompressed.CopyTo(output);
        return output.ToArray();
    }

    /// <summary> The package's images, stored on first use: the "media" entry maps each file name to a numbered entry, as JSON in older packages and protobuf in newer ones. </summary>
    private sealed class Media
    {
        private readonly ZipArchive _zip;
        private readonly Dictionary<string, string> _entryByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> _stored = new(StringComparer.Ordinal);

        public int Stored => _stored.Values.Count(name => name is not null);

        public Media(ZipArchive zip)
        {
            _zip = zip;
            if (zip.GetEntry("media") is not { } entry) return;

            try
            {
                var map = ReadEntry(entry);
                if (map is [(byte)'{', ..])
                {
                    using var json = JsonDocument.Parse(map);
                    foreach (var file in json.RootElement.EnumerateObject())
                        if (file.Value.GetString() is { } name) _entryByName.TryAdd(name, file.Name);
                }
                else
                {
                    // MediaEntries: field 1 repeats per file, whose field 1 is its name; the nth file is entry "n".
                    var files = Protobuf.ReadMessages(map, 1);
                    for (var index = 0; index < files.Count; index++)
                        if (Protobuf.ReadString(files[index], 1) is { } name) _entryByName.TryAdd(name, index.ToString());
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
            {
                Logger.LogError("Couldn't read the Anki package's media list.", ex);
            }
        }

        /// <summary> The stored image's name for a file the cards refer to, or null when it's missing or can't be read. </summary>
        public string? Store(string source)
        {
            if (_stored.TryGetValue(source, out var known)) return known;

            string? stored = null;
            var entryName = _entryByName.GetValueOrDefault(source) ?? _entryByName.GetValueOrDefault(Uri.UnescapeDataString(source));
            if (entryName is not null && _zip.GetEntry(entryName) is { } entry)
            {
                try { stored = ImageStore.Add(ReadEntry(entry)); }
                catch (ImageStoreException) { }
                catch (Exception ex) when (ex is IOException or InvalidDataException)
                {
                    Logger.LogError($"Couldn't read Anki image {source}", ex);
                }
            }

            return _stored[source] = stored;
        }
    }

    private static AnkiImportResult ImportCollection(string collectionPath, Media media)
    {
        using var anki = Db.OpenUnpooled(collectionPath, SqliteOpenMode.ReadOnly);

        var (noteTypes, deckNames) = Db.TableExists(anki, "notetypes") ? ReadSchema18(anki) : ReadLegacySchema(anki);
        var notes = ReadNotes(anki);

        // Each note goes in the deck of its first card (the home deck for cards in a filtered deck); Anki only makes a reverse card when it's wanted.
        var noteDecks = new Dictionary<long, long>();
        var notesWithReverse = new HashSet<long>();
        foreach (var (noteId, deckId, ord) in Db.Query(anki, null, "SELECT nid, CASE WHEN odid <> 0 THEN odid ELSE did END, ord FROM cards ORDER BY nid, ord",
            r => (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2))))
        {
            noteDecks.TryAdd(noteId, deckId);
            if (ord == 1) notesWithReverse.Add(noteId);
        }

        var cardsByDeck = new Dictionary<long, List<CardExportEntry>>();
        var decksWithMissingImages = new HashSet<long>();
        int withMissingImages = 0, skipped = 0;

        foreach (var note in notes)
        {
            if (!noteTypes.TryGetValue(note.NoteTypeID, out var type))
            {
                skipped++;
                continue;
            }
            if (!noteDecks.TryGetValue(note.ID, out var deckId)) continue;

            var card = ConvertNote(note, type, notesWithReverse.Contains(note.ID), media, out var missedImage);
            if (card is null)
            {
                skipped++;
                continue;
            }

            if (missedImage)
            {
                withMissingImages++;
                decksWithMissingImages.Add(deckId);
            }
            if (!cardsByDeck.TryGetValue(deckId, out var cards)) cardsByDeck[deckId] = cards = [];
            cards.Add(card);
        }

        if (cardsByDeck.Count == 0)
            throw new InvalidDataException("No cards in this file could be imported.");

        DatabaseManager.InitDatabase();
        SaveDecks(cardsByDeck, deckNames);
        return new AnkiImportResult(cardsByDeck.Count, cardsByDeck.Values.Sum(cards => cards.Count), media.Stored, withMissingImages, skipped,
            [.. decksWithMissingImages.Select(id => DeckName(deckNames, id)).Order()]);
    }

    /// <summary> Anki 2.1.28+: note types, fields, templates and decks in tables, their settings in protobuf. </summary>
    private static (Dictionary<long, NoteType>, Dictionary<long, string[]>) ReadSchema18(SqliteConnection anki)
    {
        var noteTypes = new Dictionary<long, NoteType>();
        foreach (var (id, name, config) in Db.Query(anki, null, "SELECT id, name, config FROM notetypes",
            r => (r.GetInt64(0), r.GetString(1), (byte[])r[2])))
        {
            // Notetype.Config field 1 is its kind: 0 normal, 1 cloze.
            var isCloze = Protobuf.ReadVarint(config, 1) == 1;
            noteTypes[id] = new NoteType(name, isCloze, [], []);
        }

        foreach (var (typeId, name) in Db.Query(anki, null, "SELECT ntid, name FROM fields ORDER BY ntid, ord", r => (r.GetInt64(0), r.GetString(1))))
            if (noteTypes.TryGetValue(typeId, out var type)) type.Fields.Add(name);

        foreach (var (typeId, config) in Db.Query(anki, null, "SELECT ntid, config FROM templates ORDER BY ntid, ord", r => (r.GetInt64(0), (byte[])r[1])))
        {
            // Template.Config fields 1 and 2: the question and answer formats.
            if (noteTypes.TryGetValue(typeId, out var type))
                type.Templates.Add((Protobuf.ReadString(config, 1) ?? "", Protobuf.ReadString(config, 2) ?? ""));
        }

        var decks = Db.Query(anki, null, "SELECT id, name FROM decks", r => (r.GetInt64(0), r.GetString(1)))
            .ToDictionary(deck => deck.Item1, deck => deck.Item2.Split('\x1f'));
        return (noteTypes, decks);
    }

    /// <summary> Older collections: note types and decks as JSON in the col table. </summary>
    private static (Dictionary<long, NoteType>, Dictionary<long, string[]>) ReadLegacySchema(SqliteConnection anki)
    {
        var (modelsJson, decksJson) = Db.Query(anki, null, "SELECT models, decks FROM col", r => (r.GetString(0), r.GetString(1))).First();
        var noteTypes = new Dictionary<long, NoteType>();

        using (var models = JsonDocument.Parse(modelsJson))
        {
            foreach (var model in models.RootElement.EnumerateObject())
            {
                var value = model.Value;
                var fields = value.GetProperty("flds").EnumerateArray()
                    .OrderBy(field => field.GetProperty("ord").GetInt32())
                    .Select(field => field.GetProperty("name").GetString() ?? "").ToList();
                var templates = value.GetProperty("tmpls").EnumerateArray()
                    .OrderBy(template => template.GetProperty("ord").GetInt32())
                    .Select(template => (template.GetProperty("qfmt").GetString() ?? "", template.GetProperty("afmt").GetString() ?? "")).ToList();
                var isCloze = value.TryGetProperty("type", out var kind) && kind.GetInt32() == 1;
                noteTypes[long.Parse(model.Name)] = new NoteType(value.GetProperty("name").GetString() ?? "", isCloze, fields, templates);
            }
        }

        using var decks = JsonDocument.Parse(decksJson);
        var deckNames = decks.RootElement.EnumerateObject().ToDictionary(
            deck => long.Parse(deck.Name),
            deck => (deck.Value.GetProperty("name").GetString() ?? "").Split("::"));
        return (noteTypes, deckNames);
    }

    private static List<Note> ReadNotes(SqliteConnection anki) =>
        Db.Query(anki, null, "SELECT id, mid, flds FROM notes ORDER BY id", r => new Note(r.GetInt64(0), r.GetInt64(1), r.GetString(2).Split('\x1f')));

    // --- Notes to cards ---

    /// <summary> The card for a note, or null when there's nothing to import (image occlusion, or a missing image is all it asks). </summary>
    private static CardExportEntry? ConvertNote(Note note, NoteType type, bool hasReverse, Media media, out bool missedImage)
    {
        missedImage = false;
        var (question, answer) = type.Templates.FirstOrDefault();
        question ??= "";
        answer ??= "";
        if (question.Contains("image-occlusion")) return null;

        string Field(string name)
        {
            var index = type.Fields.IndexOf(name);
            return index >= 0 && index < note.Fields.Length ? note.Fields[index] : "";
        }

        // The fields' text, converted, one paragraph each.
        var missedAny = false;
        string Join(IEnumerable<string> names)
        {
            var parts = new List<string>();
            foreach (var name in names)
            {
                var text = AnkiTextConverter.Convert(Field(name), media.Store, out var missed);
                missedAny |= missed;
                if (text.Length > 0) parts.Add(text);
            }
            return string.Join("\n\n", parts);
        }

        var questionFields = FieldsIn(question, type);
        var answerFields = FieldsIn(answer, type).Except(questionFields).ToList();

        if (type.IsCloze || note.Fields.Any(AnkiTextConverter.HasCloze))
        {
            var clozeField = ClozeFieldRegex().Match(question) is { Success: true } match ? match.Groups[1].Value.Trim() : type.Fields.FirstOrDefault() ?? "";
            var text = Join([clozeField]);
            if (!ClozeUtility.HasBlanks(text)) return null;

            var extra = Join(answerFields.Where(name => name != clozeField));
            missedImage = missedAny;
            return new CardExportEntry(nameof(ClozeFlashCard), text, extra, null, null, null, null, null, null);
        }

        // Fields the template doesn't show (or a template we can't read): the first field asks, the rest answer.
        if (questionFields.Count == 0)
        {
            questionFields = type.Fields.Take(1).ToList();
            answerFields = type.Fields.Skip(1).ToList();
        }

        var front = Join(questionFields);
        if (front.Length == 0 || front == AnkiTextConverter.ImagePlaceholder) return null;

        if (TypeFieldRegex().Match(question) is { Success: true } typed)
        {
            var typedField = typed.Groups[1].Value.Trim();
            var typedAnswer = AnkiTextConverter.ToPlainText(Field(typedField));
            if (typedAnswer.Length > 0)
            {
                if (Join(questionFields.Where(name => name != typedField)) is { Length: > 0 } withoutTyped) front = withoutTyped;
                // Shown after answering, unless it's just the typed answer again.
                var shown = Join(answerFields.Prepend(typedField).Distinct());
                if (shown == typedAnswer) shown = "";
                missedImage = missedAny;
                return new CardExportEntry(nameof(TypeFlashCard), front, shown, typedAnswer, null, null, null, null, null);
            }
        }

        var back = Join(answerFields);
        missedImage = missedAny;
        return new CardExportEntry(nameof(FlipFlashCard), front, back, null, null, null, null, null, null,
            IsReversible: hasReverse && type.Templates.Count > 1);
    }

    /// <summary> The note's fields a card template shows, in order: {{Front}}, {{type:Back}}, {{cloze:Text}} and so on. </summary>
    private static List<string> FieldsIn(string template, NoteType type) =>
        [.. FieldReferenceRegex().Matches(template)
            .Select(match => match.Groups[1].Value.Split(':')[^1].Trim())
            .Where(type.Fields.Contains)
            .Distinct()];

    private static void SaveDecks(Dictionary<long, List<CardExportEntry>> cardsByDeck, Dictionary<long, string[]> deckNames)
    {
        Db.InTransaction((connection, transaction) =>
        {
            foreach (var (ankiDeckId, cards) in cardsByDeck)
            {
                var parents = deckNames.GetValueOrDefault(ankiDeckId)?[..^1] ?? [];
                var folderID = FolderRepository.EnsureFolderPath(connection, transaction, null, parents.Select(name => name.Trim()).Where(name => name.Length > 0));
                DeckRepository.InsertDeckWithCards(connection, transaction, DeckName(deckNames, ankiDeckId), folderID, cards.Select(FlashCardFactory.FromExportEntry));
            }
        });
    }

    /// <summary> The ReviFlash deck's name: the last part of the Anki deck's ("Maths::Calculus" -> "Calculus"). </summary>
    private static string DeckName(Dictionary<long, string[]> deckNames, long id) =>
        deckNames.GetValueOrDefault(id)?[^1].Trim() is { Length: > 0 } name ? name : "Anki Import";

    // {{Field}} and {{filter:Field}}, but not {{#Section}}, {{/Section}}, {{^Inverted}} or comments.
    [GeneratedRegex(@"\{\{(?![#/^!])([^{}]+)\}\}")]
    private static partial Regex FieldReferenceRegex();

    [GeneratedRegex(@"\{\{type:(?:cloze:)?([^{}]+)\}\}")]
    private static partial Regex TypeFieldRegex();

    [GeneratedRegex(@"\{\{(?:[^{}:]+:)*cloze:([^{}]+)\}\}")]
    private static partial Regex ClozeFieldRegex();

    /// <summary> Just enough protobuf to read Anki's note type and template settings. </summary>
    private static class Protobuf
    {
        public static long? ReadVarint(byte[] data, int field) =>
            Find(data, field, 0) is { } value ? (long)value.Varint : null;

        public static string? ReadString(byte[] data, int field) =>
            Find(data, field, 2) is { } value ? Encoding.UTF8.GetString(data, value.Start, value.Length) : null;

        /// <summary> Every copy of a repeated message field, in order. </summary>
        public static List<byte[]> ReadMessages(byte[] data, int field) =>
            [.. Fields(data).Where(value => value.Number == field && value.WireType == 2).Select(value => data[value.Start..(value.Start + value.Length)])];

        private static (ulong Varint, int Start, int Length)? Find(byte[] data, int field, int wireType)
        {
            foreach (var value in Fields(data))
            {
                if (value.Number == field && value.WireType == wireType) return (value.Varint, value.Start, value.Length);
            }
            return null;
        }

        private static IEnumerable<(int Number, int WireType, ulong Varint, int Start, int Length)> Fields(byte[] data)
        {
            var i = 0;
            while (i < data.Length)
            {
                var key = Varint(data, ref i);
                var type = (int)(key & 7);
                var number = (int)(key >> 3);

                switch (type)
                {
                    case 0:
                        yield return (number, 0, Varint(data, ref i), 0, 0);
                        break;
                    case 1:
                        i += 8;
                        break;
                    case 2:
                        var length = (int)Varint(data, ref i);
                        yield return (number, 2, 0, i, length);
                        i += length;
                        break;
                    case 5:
                        i += 4;
                        break;
                    default:
                        yield break; // groups (3, 4) aren't used by Anki
                }
            }
        }

        private static ulong Varint(byte[] data, ref int i)
        {
            ulong result = 0;
            for (var shift = 0; i < data.Length; shift += 7)
            {
                var b = data[i++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
            }
            return result;
        }
    }
}
