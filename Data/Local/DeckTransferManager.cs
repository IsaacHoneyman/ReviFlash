using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

public static class DeckTransferManager
{
    public static void TryCreateDeckExport(string destinationFilePath, IReadOnlyCollection<ulong> deckIds)
    {
        if (deckIds.Count == 0) throw new ArgumentException("At least one deck must be selected for export.");

        var exportData = BuildExportPackage(deckIds.Distinct().ToList());
        if (exportData.Decks.Count == 0) throw new InvalidOperationException("None of the selected decks could be exported.");

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

        using var zip = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("flashcards.json");
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    public static int TryImportDeckExport(string zipFilePath)
    {
        if (!File.Exists(zipFilePath)) throw new FileNotFoundException("The specified export file does not exist.");

        using var zip = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        var exportEntry = archive.GetEntry("flashcards.json") ?? throw new InvalidDataException("The selected file is not a valid ReviFlash flashcard export.");
        using var entryStream = exportEntry.Open();
        var package = JsonSerializer.Deserialize<FlashCardExportPackage>(entryStream)
            ?? throw new InvalidDataException("The export file could not be read.");

        if (package.Decks.Count == 0) throw new InvalidDataException("The export file does not contain any decks.");

        DatabaseManager.InitDatabase();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        int importedDeckCount = 0;
        foreach (var deck in package.Decks)
        {
            long deckId = DeckRepository.InsertDeck(connection, transaction, deck.Name);

            foreach (var card in deck.Cards)
            {
                long cardId = DeckRepository.InsertCard(connection, transaction, deckId, card);

                if (card.Options is not null)
                {
                    foreach (var (index, option) in card.Options.Select((value, index) => (index, value)))
                        DeckRepository.InsertMultiChoiceOption(connection, transaction, cardId, index, option);
                }

                if (card.Pairs is not null)
                {
                    foreach (var (index, pair) in card.Pairs.Select((value, index) => (index, value)))
                        DeckRepository.InsertMatchPair(connection, transaction, cardId, index, pair);
                }
            }

            foreach (var stat in deck.Stats) DeckRepository.InsertDeckStat(connection, transaction, deckId, stat);
            importedDeckCount++;
        }

        transaction.Commit();
        return importedDeckCount;
    }

    // --- Online Generation ---

    public static string GenerateCloudExportJson(ulong deckId)
    {
        var exportData = BuildExportPackage([deckId]);
        var deckExport = exportData.Decks.FirstOrDefault() ?? throw new InvalidOperationException("Failed to generate export package.");
        var payload = new
        {
            ExportVersion = 1,
            DeckName = deckExport.Name,
            Cards = deckExport.Cards
        };

        return JsonSerializer.Serialize(payload, TextUtility.Indented);
    }

    public static void TryImportCloudDeck(string jsonPayload)
    {
        using var document = JsonDocument.Parse(jsonPayload);
        var root = document.RootElement;

        string deckName = root.TryGetProperty("DeckName", out var nameProp)
            ? nameProp.GetString() ?? "Imported Cloud Deck" : "Imported Cloud Deck";

        var cards = root.TryGetProperty("Cards", out var cardsProp)
            ? JsonSerializer.Deserialize<List<CardExportEntry>>(cardsProp.GetRawText(), TextUtility.Indented) ?? [] : [];

        if (cards.Count == 0)
            throw new InvalidDataException("The downloaded deck does not contain any readable cards.");

        DatabaseManager.InitDatabase();
        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        long deckId = DeckRepository.InsertDeck(connection, transaction, deckName);

        foreach (var card in cards)
        {
            long cardId = DeckRepository.InsertCard(connection, transaction, deckId, card);

            if (card.Options is not null)
                foreach (var (index, option) in card.Options.Select((value, i) => (i, value)))
                    DeckRepository.InsertMultiChoiceOption(connection, transaction, cardId, index, option);


            if (card.Pairs is not null)
                foreach (var (index, pair) in card.Pairs.Select((value, i) => (i, value)))
                    DeckRepository.InsertMatchPair(connection, transaction, cardId, index, pair);
        }

        transaction.Commit();
    }

    // --- Helper ---

    private static FlashCardExportPackage BuildExportPackage(IReadOnlyCollection<ulong> deckIds)
    {
        List<DeckExportEntry> decks = [];

        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        foreach (var deckId in deckIds)
        {
            var deckName = DeckRepository.GetDeckName(connection, deckId);
            if (deckName is null) continue;
            decks.Add(new DeckExportEntry(deckName, DeckRepository.LoadDeckCards(connection, deckId), []));
        }

        return new FlashCardExportPackage(decks);
    }

    public static object BuildExportAnswerPayload(CardExportEntry card)
    {
        return card.CardType switch
        {
            nameof(TypeFlashCard) => card.Answer ?? card.Back,
            nameof(TrueFalseFlashCard) => JsonSerializer.Serialize(new TrueFalseAnswerPayload(
                card.CorrectAnswerIsTrue ?? true,
                string.IsNullOrWhiteSpace(card.TrueLabel) ? "True" : card.TrueLabel!,
                string.IsNullOrWhiteSpace(card.FalseLabel) ? "False" : card.FalseLabel!)),
            _ => DBNull.Value,
        };
    }

    public static CardExportEntry BuildTrueFalseExportEntry(string front, string back, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);

        if (bool.TryParse(answer, out var parsedBool))
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, parsedBool, "True", "False", null, null);

        try
        {
            var payload = JsonSerializer.Deserialize<TrueFalseAnswerPayload>(answer);
            if (payload is null) return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);

            return new CardExportEntry(
                nameof(TrueFalseFlashCard), front, back, null, payload.CorrectAnswerIsTrue,
                payload.TrueLabel, payload.FalseLabel, null, null);
        }
        catch (JsonException)
        {
            return new CardExportEntry(nameof(TrueFalseFlashCard), front, back, null, true, "True", "False", null, null);
        }
    }
}