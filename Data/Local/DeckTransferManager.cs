using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

public static class DeckTransferManager
{
    // --- Online Generation ---

    /// <summary> Names of the folder and its ancestors, outermost first. Empty for the main menu. </summary>
    public static List<string> GetFolderPathNames(ulong? folderID) =>
        FolderTree.Load().AncestorChain(folderID).Select(folder => folder.Name).ToList();

    /// <param name="folderPath"> Folder names (outermost first) to travel with the upload; empty to leave them out. </param>
    public static string GenerateCloudExportJson(ulong deckId, IReadOnlyList<string> folderPath)
    {
        using var connection = DatabaseManager.GetConnection();
        connection.Open();

        var deckName = DeckRepository.GetDeckName(connection, deckId) ?? throw new InvalidOperationException("Failed to generate export package.");
        var payload = new
        {
            ExportVersion = 1,
            DeckName = deckName,
            FolderPath = folderPath,
            Cards = DeckRepository.LoadDeckCards(connection, deckId)
        };

        return JsonSerializer.Serialize(payload, TextUtility.Indented);
    }

    /// <param name="targetFolderID"> Folder the downloaded set is filed into; null for the main menu. </param>
    /// <param name="recreateFolders"> Rebuild the uploader's folder path beneath <paramref name="targetFolderID"/>. </param>
    public static void TryImportCloudDeck(string jsonPayload, ulong? targetFolderID = null, bool recreateFolders = false)
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

        // Uploads from before 1.1 carry no FolderPath and land directly in the target folder.
        if (recreateFolders && root.TryGetProperty("FolderPath", out var pathProp) && pathProp.ValueKind == JsonValueKind.Array)
        {
            var folderNames = pathProp.EnumerateArray()
                .Where(name => name.ValueKind == JsonValueKind.String)
                .Select(name => name.GetString()!.Trim())
                .Where(name => name.Length > 0);
            targetFolderID = EnsureFolderPath(targetFolderID, folderNames);
        }

        using var connection = DatabaseManager.GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        long deckId = DeckRepository.InsertDeck(connection, transaction, deckName, targetFolderID);

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

    /// <summary> Walks down from <paramref name="parentFolderID"/>, reusing same-named folders and creating the rest. </summary>
    internal static ulong? EnsureFolderPath(ulong? parentFolderID, IEnumerable<string> folderNames)
    {
        var tree = FolderTree.Load();
        bool creating = false;

        foreach (var name in folderNames)
        {
            var existing = creating ? null : tree.ChildrenOf(parentFolderID)
                .FirstOrDefault(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing is null) creating = true;
            parentFolderID = existing?.ID ?? FolderRepository.CreateFolder(name, parentFolderID).ID;
        }

        return parentFolderID;
    }

    public static object BuildExportAnswerPayload(CardExportEntry card)
    {
        return card.CardType switch
        {
            nameof(TypeFlashCard) => card.Answer ?? card.Back,
            nameof(ClozeFlashCard) => card.Answer ?? (object)DBNull.Value,
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