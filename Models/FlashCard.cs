using System;
using System.Collections.Generic;

using static ReviFlash.Utilities.CardUtility;

namespace ReviFlash.Models;

public abstract class FlashCard(string front, string back)
{
    public ulong ID { get; protected set; } = ulong.MaxValue;
    public string Front { get; private set; } = front;
    public string Back { get; private set; } = back;

    /// <summary> What a review calls a question made from another card (e.g. a cloze blank), instead of its own type. </summary>
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

    /// <summary> The label the card editor shows for this card. </summary>
    public string CardType => this switch
    {
        TypeFlashCard => CARD_TYPE_TYPE,
        FlipFlashCard { IsReversible: true } => $"{CARD_TYPE_FLIP} ↔",
        FlipFlashCard => CARD_TYPE_FLIP,
        ClozeFlashCard => CARD_TYPE_CLOZE,
        MultiFlashCard => CARD_TYPE_MULTI_CHOICE,
        MatchFlashCard => CARD_TYPE_MATCH,
        TrueFalseFlashCard => CARD_TYPE_TRUE_FALSE,
        _ => "Unknown"
    };

    public bool IsMultiChoiceCard => this is MultiFlashCard;
    public bool IsMatchCard => this is MatchFlashCard;
    public bool IsTrueFalseCard => this is TrueFalseFlashCard;

    public virtual IReadOnlyList<MultiChoicePreviewOption> MultiChoiceOptionsPreview => [];
    public virtual IReadOnlyList<MatchPreviewPair> MatchPairsPreview => [];
}