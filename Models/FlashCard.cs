using System;
using System.Collections.Generic;

namespace ReviFlash.Models;

public abstract class FlashCard(string front, string back)
{
    public ulong ID { get; protected set; } = ulong.MaxValue;
    public string Front { get; private set; } = front;
    public string Back { get; private set; } = back;

    /// <summary> Type label a review shows for a question made from another card, e.g. a cloze blank. </summary>
    public string? ReviewLabel { get; init; }

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

    /// <summary> The card type's name, as the editor's type picker lists it. </summary>
    public abstract string TypeName { get; }

    /// <summary> The label the card editor's card list shows for this card. </summary>
    public virtual string CardType => TypeName;

    /// <summary> The type label a review shows for this question. </summary>
    public virtual string ReviewTypeLabel => ReviewLabel ?? TypeName;

    public bool IsMultiChoiceCard => this is MultiFlashCard;
    public bool IsMatchCard => this is MatchFlashCard;
    public bool IsTrueFalseCard => this is TrueFalseFlashCard;

    public virtual IReadOnlyList<MultiChoicePreviewOption> MultiChoiceOptionsPreview => [];
    public virtual IReadOnlyList<MatchPreviewPair> MatchPairsPreview => [];
}