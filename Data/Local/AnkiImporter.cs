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

/// <summary> Import counts: image cards keep "[image]" placeholders; Skipped notes are image occlusion or media only. </summary>
public sealed record AnkiImportResult(int Decks, int Cards, int CardsWithImages, int Skipped, IReadOnlyList<string> DecksWithImages);

/// <summary> Imports an Anki .apkg/.colpkg as decks (parent decks become folders) of Flip, Type and Cloze cards, without scheduling. </summary>
public static partial class AnkiImporter
{
    private sealed record NoteType(string Name, bool IsCloze, List<string> Fields, List<(string Question, string Answer)> Templates);

    private sealed record Note(long ID, long NoteTypeID, string[] Fields);

    public static AnkiImportResult Import(string packagePath)
    {
        var collectionPath = ExtractCollection(packagePath);
        try
        {
            return ImportCollection(collectionPath);
        }
        finally
        {
            File.Delete(collectionPath);
        }
    }

    // --- Reading the package ---

    /// <summary> Copies the collection out of the zip, newest format first: newer packages also hold a stub "please update Anki" collection.anki2. </summary>
    private static string ExtractCollection(string packagePath)
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
        var target = Path.Combine(Path.GetTempPath(), $"reviflash-anki-{Guid.NewGuid():N}.db");

        if (zip.GetEntry("collection.anki21b") is { } compressed)
        {
            using var input = compressed.Open();
            using var decompressed = new ZstdSharp.DecompressionStream(input);
            using var output = File.Create(target);
            decompressed.CopyTo(output);
            return target;
        }

        var legacy = zip.GetEntry("collection.anki21") ?? zip.GetEntry("collection.anki2")
            ?? throw new InvalidDataException("This file doesn't contain an Anki collection. Choose an .apkg exported from Anki.");
        legacy.ExtractToFile(target);
        return target;
    }

    private static AnkiImportResult ImportCollection(string collectionPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = collectionPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false, // so the temporary copy can be deleted straight after
        }.ToString();

        using var anki = new SqliteConnection(connectionString);
        anki.Open();

        var (noteTypes, deckNames) = HasTable(anki, "notetypes") ? ReadSchema18(anki) : ReadLegacySchema(anki);
        var notes = ReadNotes(anki);

        // Each note goes in the deck of its first card; Anki only makes a reverse card when it's wanted.
        var noteDecks = new Dictionary<long, long>();
        var notesWithReverse = new HashSet<long>();
        using (var command = anki.CreateCommand())
        {
            command.CommandText = "SELECT nid, did, ord FROM cards ORDER BY nid, ord";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var noteId = reader.GetInt64(0);
                noteDecks.TryAdd(noteId, reader.GetInt64(1));
                if (reader.GetInt64(2) == 1) notesWithReverse.Add(noteId);
            }
        }

        var cardsByDeck = new Dictionary<long, List<CardExportEntry>>();
        var decksWithImages = new HashSet<long>();
        int withImages = 0, skipped = 0;

        foreach (var note in notes)
        {
            if (!noteTypes.TryGetValue(note.NoteTypeID, out var type) || !noteDecks.TryGetValue(note.ID, out var deckId)) continue;

            var card = ConvertNote(note, type, notesWithReverse.Contains(note.ID), out var hadImage);
            if (card is null)
            {
                skipped++;
                continue;
            }

            if (hadImage)
            {
                withImages++;
                decksWithImages.Add(deckId);
            }
            if (!cardsByDeck.TryGetValue(deckId, out var cards)) cardsByDeck[deckId] = cards = [];
            cards.Add(card);
        }

        if (cardsByDeck.Count == 0)
            throw new InvalidDataException("No cards in this file could be imported.");

        DatabaseManager.InitDatabase();
        SaveDecks(cardsByDeck, deckNames);
        return new AnkiImportResult(cardsByDeck.Count, cardsByDeck.Values.Sum(cards => cards.Count), withImages, skipped,
            [.. decksWithImages.Select(id => DeckName(deckNames, id)).Order()]);
    }

    private static bool HasTable(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return (long)command.ExecuteScalar()! > 0;
    }

    /// <summary> Anki 2.1.28+: note types, fields, templates and decks in tables, their settings in protobuf. </summary>
    private static (Dictionary<long, NoteType>, Dictionary<long, string[]>) ReadSchema18(SqliteConnection anki)
    {
        var noteTypes = new Dictionary<long, NoteType>();
        foreach (var (id, name, config) in Query(anki, "SELECT id, name, config FROM notetypes",
            r => (r.GetInt64(0), r.GetString(1), (byte[])r[2])))
        {
            // Notetype.Config field 1 is its kind: 0 normal, 1 cloze.
            var isCloze = Protobuf.ReadVarint(config, 1) == 1;
            noteTypes[id] = new NoteType(name, isCloze, [], []);
        }

        foreach (var (typeId, name) in Query(anki, "SELECT ntid, name FROM fields ORDER BY ntid, ord", r => (r.GetInt64(0), r.GetString(1))))
            if (noteTypes.TryGetValue(typeId, out var type)) type.Fields.Add(name);

        foreach (var (typeId, config) in Query(anki, "SELECT ntid, config FROM templates ORDER BY ntid, ord", r => (r.GetInt64(0), (byte[])r[1])))
        {
            // Template.Config fields 1 and 2: the question and answer formats.
            if (noteTypes.TryGetValue(typeId, out var type))
                type.Templates.Add((Protobuf.ReadString(config, 1) ?? "", Protobuf.ReadString(config, 2) ?? ""));
        }

        var decks = Query(anki, "SELECT id, name FROM decks", r => (r.GetInt64(0), r.GetString(1)))
            .ToDictionary(deck => deck.Item1, deck => deck.Item2.Split('\x1f'));
        return (noteTypes, decks);
    }

    /// <summary> Older collections: note types and decks as JSON in the col table. </summary>
    private static (Dictionary<long, NoteType>, Dictionary<long, string[]>) ReadLegacySchema(SqliteConnection anki)
    {
        var (modelsJson, decksJson) = Query(anki, "SELECT models, decks FROM col", r => (r.GetString(0), r.GetString(1))).First();
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
        Query(anki, "SELECT id, mid, flds FROM notes ORDER BY id", r => new Note(r.GetInt64(0), r.GetInt64(1), r.GetString(2).Split('\x1f')));

    private static List<T> Query<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> read)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(read(reader));
        return rows;
    }

    // --- Notes to cards ---

    /// <summary> The card for a note, or null when there's nothing to import (image occlusion, image-only notes). </summary>
    private static CardExportEntry? ConvertNote(Note note, NoteType type, bool hasReverse, out bool hadImage)
    {
        hadImage = false;
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
        var images = false;
        string Join(IEnumerable<string> names)
        {
            var parts = new List<string>();
            foreach (var name in names)
            {
                var text = AnkiTextConverter.Convert(Field(name), out var image);
                images |= image;
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
            hadImage = images;
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
                hadImage = images;
                return new CardExportEntry(nameof(TypeFlashCard), front, shown, typedAnswer, null, null, null, null, null);
            }
        }

        var back = Join(answerFields);
        hadImage = images;
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
        // Folders first (they're created through their own repository), then all the cards in one transaction.
        var folders = cardsByDeck.Keys.ToDictionary(id => id, id =>
        {
            var parents = deckNames.GetValueOrDefault(id)?[..^1] ?? [];
            return (Folder: DeckTransferManager.EnsureFolderPath(null, parents.Select(name => name.Trim()).Where(name => name.Length > 0)),
                Name: DeckName(deckNames, id));
        });

        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var (ankiDeckId, cards) in cardsByDeck)
        {
            var (folder, name) = folders[ankiDeckId];
            var deckId = DeckRepository.InsertDeck(connection, transaction, name, folder);
            foreach (var card in cards) DeckRepository.InsertCard(connection, transaction, deckId, card);
        }

        transaction.Commit();
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

        private static (ulong Varint, int Start, int Length)? Find(byte[] data, int field, int wireType)
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
                        var value = Varint(data, ref i);
                        if (number == field && wireType == 0) return (value, 0, 0);
                        break;
                    case 1:
                        i += 8;
                        break;
                    case 2:
                        var length = (int)Varint(data, ref i);
                        if (number == field && wireType == 2) return (0, i, length);
                        i += length;
                        break;
                    case 5:
                        i += 4;
                        break;
                    default:
                        return null; // groups (3, 4) aren't used by Anki
                }
            }
            return null;
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
