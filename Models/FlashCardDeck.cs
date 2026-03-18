using System.Collections.Generic;

namespace ReviFlash.Models;

public class FlashCardDeck
{
    public static ulong DeckIDCounter = 0;

    private static ulong GenerateID()
    {
        return DeckIDCounter++;
    }

    public ulong ID { get; private set; }
    public string Name { get; private set; }
    private readonly List<FlashCard> flashCards = [];
    public IReadOnlyList<FlashCard> FlashCards => flashCards.AsReadOnly();

    public FlashCardDeck(string name)
    {
        Name = name;
        ID = GenerateID();
    }

    public FlashCardDeck(string name, ulong id)
    {
        Name = name;
        ID = id;
    }

    public void AddFlashCard(FlashCard card)
    {
        flashCards.Add(card);
    }

    public void RemoveFlashCard(FlashCard card)
    {
        flashCards.Remove(card);
    }
}