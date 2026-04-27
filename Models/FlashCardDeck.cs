using System;
using System.Collections.Generic;

namespace ReviFlash.Models;

public class FlashCardDeck
{
    public ulong ID { get; private set; }
    public string Name { get; set; }
    public int CardCount { get; set; }
    public bool IsSelectedForMultiReview { get; set; }
    private readonly List<FlashCard> flashCards = [];
    public IReadOnlyList<FlashCard> FlashCards => flashCards.AsReadOnly();

    public FlashCardDeck(string name)
    {
        Name = name;
        ID = ulong.MaxValue; // Placeholder ID until saved to database
    }

    public FlashCardDeck(string name, ulong id, int cardCount) : this(name)
    {
        ID = id;
        CardCount = cardCount;
    }

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
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