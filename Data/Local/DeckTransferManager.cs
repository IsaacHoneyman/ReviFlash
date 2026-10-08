using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        using var connection = Db.Open();

        var deckName = DeckRepository.GetDeckName(connection, deckId) ?? throw new InvalidOperationException("Failed to generate export package.");
        var payload = new
        {
            ExportVersion = 1,
            DeckName = deckName,
            FolderPath = folderPath,
            Cards = DeckRepository.LoadCards(connection, deckId).Select(FlashCardFactory.ToExportEntry).ToList()
        };

        return JsonSerializer.Serialize(payload, TextUtility.Indented);
    }

    /// <summary> Imports a downloaded deck into the target folder (null for the main menu), optionally rebuilding the uploader's folders beneath it. </summary>
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

        var flashCards = cards.Select(FlashCardFactory.FromExportEntry).ToList();

        // Uploads without a FolderPath land directly in the target folder.
        List<string> folderNames = recreateFolders && root.TryGetProperty("FolderPath", out var pathProp) && pathProp.ValueKind == JsonValueKind.Array
            ? [.. pathProp.EnumerateArray()
                .Where(name => name.ValueKind == JsonValueKind.String)
                .Select(name => name.GetString()!.Trim())
                .Where(name => name.Length > 0)]
            : [];

        DatabaseManager.InitDatabase();

        Db.InTransaction((connection, transaction) =>
        {
            var folderID = FolderRepository.EnsureFolderPath(connection, transaction, targetFolderID, folderNames);
            DeckRepository.InsertDeckWithCards(connection, transaction, deckName, folderID, flashCards);
        });
    }
}
