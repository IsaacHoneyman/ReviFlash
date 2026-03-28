using System;
using System.Collections.Generic;

namespace ReviFlash.Models;

public abstract class FlashCard
{
    public ulong ID {get; protected set; }
    public string Front { get; private set; }
    public string Back { get; private set; }
    
    public FlashCard(string front, string back)
    {
        Front = front;
        Back = back;
        ID = ulong.MaxValue; // Placeholder ID until saved to database
    }

    public void AssignDatabaseID(ulong id)
    {
        if (ID == ulong.MaxValue) ID = id;
        else throw new InvalidOperationException("ID has already been assigned.");
    }

    public void UpdateContent(string front, string back)
    {
        Front = front;
        Back = back;
    }

    public abstract bool VerifyAnswer(object answer);

    public string CardType => GetType().Name switch
    {
        nameof(TypeFlashCard) => "Type to Answer",
        nameof(FlipFlashCard) => "Flip",
        nameof(MultiFlashCard) => "Multi Choice",
        nameof(MatchFlashCard) => "Match",
        nameof(TrueFalseFlashCard) => "True / False",
        _ => "Unknown"
    };

    public bool IsMultiChoiceCard => this is MultiFlashCard;
    public bool IsMatchCard => this is MatchFlashCard;
    public bool IsTrueFalseCard => this is TrueFalseFlashCard;

    public virtual IReadOnlyList<MultiChoicePreviewOption> MultiChoiceOptionsPreview => [];
    public virtual IReadOnlyList<MatchPreviewPair> MatchPairsPreview => [];
}