using System;

namespace ReviFlash.Models;

public class StudyGroup(string name)
{
    public ulong ID { get; private set; } = ulong.MaxValue;
    public string Name { get; set; } = name;
    public int CardCount { get; set; }
    public int DeckCount { get; set; }
    public bool IsSelectedForMultiReview { get; set; }

    public StudyGroup(string name, ulong id, int deckCount, int cardCount) : this(name)
    {
        ID = id;
        DeckCount = deckCount;
        CardCount = cardCount;
    }

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
    }
}