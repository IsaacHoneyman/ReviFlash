using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

/// <summary> Deck and card rows on an open connection, shared by the repositories and the importers. </summary>
public static class DeckRepository
{
    // --- Read ---

    public static string? GetDeckName(SqliteConnection connection, ulong deckId) =>
        Db.Scalar<string>(connection, null, "SELECT Name FROM Decks WHERE ID = $deckId;", ("$deckId", deckId));

    /// <summary> A deck's cards in creation order, with their options and pairs. </summary>
    public static List<FlashCard> LoadCards(SqliteConnection connection, ulong deckId)
    {
        var options = Db.Query(connection, null, @"
            SELECT o.CardID, o.OptionText, o.IsCorrect
            FROM CardOptions o
            INNER JOIN Cards c ON c.ID = o.CardID
            WHERE c.DeckID = $deckId
            ORDER BY o.CardID, o.OptionIndex ASC;",
            reader => (CardId: (ulong)reader.GetInt64(0), Option: (reader.GetString(1), reader.GetInt32(2) == 1)),
            ("$deckId", deckId)).ToLookup(row => row.CardId, row => row.Option);

        var pairs = Db.Query(connection, null, @"
            SELECT p.CardID, p.LeftText, p.RightText
            FROM MatchCardPairs p
            INNER JOIN Cards c ON c.ID = p.CardID
            WHERE c.DeckID = $deckId
            ORDER BY p.CardID, p.PairIndex ASC;",
            reader => (CardId: (ulong)reader.GetInt64(0), Pair: (reader.GetString(1), reader.GetString(2))),
            ("$deckId", deckId)).ToLookup(row => row.CardId, row => row.Pair);

        return Db.Query(connection, null, @"
            SELECT ID, CardType, Front, Back, Answer, IsReversible
            FROM Cards
            WHERE DeckID = $deckId
            ORDER BY ID ASC;",
            reader =>
            {
                ulong cardId = (ulong)reader.GetInt64(0);
                return FlashCardFactory.CreateCard(
                    reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), cardId,
                    [.. options[cardId]], [.. pairs[cardId]], reader.GetInt64(5) != 0);
            },
            ("$deckId", deckId));
    }

    // --- Write ---

    /// <param name="folderID"> Folder the deck is filed into; null for the main menu. </param>
    public static ulong InsertDeck(SqliteConnection connection, SqliteTransaction? transaction, string name, ulong? folderID = null) =>
        Db.Insert(connection, transaction, "INSERT INTO Decks (Name, FolderID) VALUES ($name, $folderId);",
            ("$name", name), ("$folderId", folderID));

    public static ulong InsertDeckWithCards(SqliteConnection connection, SqliteTransaction transaction, string name, ulong? folderID, IEnumerable<FlashCard> cards)
    {
        ulong deckId = InsertDeck(connection, transaction, name, folderID);
        foreach (var card in cards) InsertCard(connection, transaction, deckId, card);
        return deckId;
    }

    /// <summary> Inserts the card with its options or pairs and returns its new ID. </summary>
    public static ulong InsertCard(SqliteConnection connection, SqliteTransaction transaction, ulong deckId, FlashCard card)
    {
        ulong cardId = Db.Insert(connection, transaction, @"
            INSERT INTO Cards (DeckID, CardType, Front, Back, Answer, IsReversible)
            VALUES ($deckId, $cardType, $front, $back, $answer, $isReversible);",
            [("$deckId", deckId), .. CardValues(card)]);

        InsertCardChildren(connection, transaction, cardId, card);
        return cardId;
    }

    /// <summary> Rewrites the card's row and replaces its options and pairs. </summary>
    public static void UpdateCard(SqliteConnection connection, SqliteTransaction transaction, FlashCard card)
    {
        Db.Execute(connection, transaction, @"
            UPDATE Cards SET CardType = $cardType, Front = $front, Back = $back, Answer = $answer, IsReversible = $isReversible WHERE ID = $id;
            DELETE FROM CardOptions WHERE CardID = $id;
            DELETE FROM MatchCardPairs WHERE CardID = $id;",
            [("$id", card.ID), .. CardValues(card)]);

        InsertCardChildren(connection, transaction, card.ID, card);
    }

    private static (string Name, object? Value)[] CardValues(FlashCard card) =>
    [
        ("$cardType", card.GetType().Name),
        ("$front", card.Front),
        ("$back", card.Back),
        ("$answer", FlashCardFactory.BuildAnswerPayload(card)),
        ("$isReversible", card is FlipFlashCard { IsReversible: true } ? 1 : 0),
    ];

    private static void InsertCardChildren(SqliteConnection connection, SqliteTransaction transaction, ulong cardId, FlashCard card)
    {
        if (card is MultiFlashCard multiCard)
        {
            for (int i = 0; i < multiCard.Options.Count; i++)
            {
                var (optionText, isCorrect) = multiCard.Options[i];
                Db.Execute(connection, transaction, @"
                    INSERT INTO CardOptions (CardID, OptionIndex, OptionText, IsCorrect)
                    VALUES ($cardId, $index, $text, $isCorrect);",
                    ("$cardId", cardId), ("$index", i), ("$text", optionText), ("$isCorrect", isCorrect ? 1 : 0));
            }
        }

        if (card is MatchFlashCard matchCard)
        {
            for (int i = 0; i < matchCard.Options.Count; i++)
            {
                var (leftText, rightText) = matchCard.Options[i];
                Db.Execute(connection, transaction, @"
                    INSERT INTO MatchCardPairs (CardID, PairIndex, LeftText, RightText)
                    VALUES ($cardId, $index, $leftText, $rightText);",
                    ("$cardId", cardId), ("$index", i), ("$leftText", leftText), ("$rightText", rightText));
            }
        }
    }
}
