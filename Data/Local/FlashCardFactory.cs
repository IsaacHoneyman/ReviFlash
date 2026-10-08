using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

public static class FlashCardFactory
{
    /// <summary> Stored in a cloze card's Answer column when its blanks are typed rather than revealed. </summary>
    public const string ClozeTypeAnswerPayload = "type";

    public static FlashCard CreateCard(string cardType, string front, string back, string? answer, ulong id,
    List<(string optionText, bool isCorrect)> options, List<(string leftText, string rightText)> pairs, bool isReversible = false)
    {
        return cardType switch
        {
            nameof(TypeFlashCard) => new TypeFlashCard(front, back, answer, id),
            nameof(FlipFlashCard) => new FlipFlashCard(front, back, id, isReversible),
            nameof(ClozeFlashCard) => new ClozeFlashCard(front, back, answer == ClozeTypeAnswerPayload, id),
            nameof(MultiFlashCard) => new MultiFlashCard(front, back, options, id),
            nameof(MatchFlashCard) => new MatchFlashCard(front, back, pairs, id),
            nameof(TrueFalseFlashCard) => BuildTrueFalseCard(front, back, answer, id),
            _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
        };
    }

    /// <summary> What the card stores in the Answer column. </summary>
    public static string? BuildAnswerPayload(FlashCard card)
    {
        return card switch
        {
            TypeFlashCard typeCard => typeCard.Answer,
            ClozeFlashCard { TypeAnswer: true } => ClozeTypeAnswerPayload,
            TrueFalseFlashCard trueFalseCard => JsonSerializer.Serialize(
                new TrueFalseAnswerPayload(trueFalseCard.CorrectAnswerIsTrue, trueFalseCard.TrueLabel, trueFalseCard.FalseLabel)),
            _ => null,
        };
    }

    // --- Transfer ---

    /// <summary> An unsaved card from an imported entry. </summary>
    public static FlashCard FromExportEntry(CardExportEntry entry)
    {
        return entry.CardType switch
        {
            nameof(TypeFlashCard) => new TypeFlashCard(entry.Front, entry.Back, entry.Answer),
            nameof(FlipFlashCard) => new FlipFlashCard(entry.Front, entry.Back, entry.IsReversible == true),
            nameof(ClozeFlashCard) => new ClozeFlashCard(entry.Front, entry.Back, entry.Answer == ClozeTypeAnswerPayload),
            nameof(MultiFlashCard) => new MultiFlashCard(entry.Front, entry.Back,
                [.. (entry.Options ?? []).Select(option => (option.OptionText, option.IsCorrect))]),
            nameof(MatchFlashCard) => new MatchFlashCard(entry.Front, entry.Back,
                [.. (entry.Pairs ?? []).Select(pair => (pair.LeftText, pair.RightText))]),
            nameof(TrueFalseFlashCard) => BuildTrueFalseCard(entry.Front, entry.Back,
                NormaliseTrueFalse(entry.CorrectAnswerIsTrue ?? true, entry.TrueLabel, entry.FalseLabel), ulong.MaxValue),
            _ => throw new InvalidOperationException($"Unknown card type: {entry.CardType}")
        };
    }

    public static CardExportEntry ToExportEntry(FlashCard card)
    {
        string cardType = card.GetType().Name;
        return card switch
        {
            TypeFlashCard typeCard => new CardExportEntry(cardType, card.Front, card.Back, typeCard.Answer, null, null, null, null, null),
            FlipFlashCard flipCard => new CardExportEntry(cardType, card.Front, card.Back, null, null, null, null, null, null, flipCard.IsReversible ? true : null),
            ClozeFlashCard => new CardExportEntry(cardType, card.Front, card.Back, BuildAnswerPayload(card), null, null, null, null, null),
            MultiFlashCard multiCard => new CardExportEntry(cardType, card.Front, card.Back, null, null, null, null,
                [.. multiCard.Options.Select(option => new MultiChoiceOptionEntry(option.optionText, option.isCorrect))], null),
            MatchFlashCard matchCard => new CardExportEntry(cardType, card.Front, card.Back, null, null, null, null, null,
                [.. matchCard.Options.Select(pair => new MatchPairEntry(pair.leftText, pair.rightText))]),
            TrueFalseFlashCard trueFalseCard => new CardExportEntry(cardType, card.Front, card.Back, null,
                trueFalseCard.CorrectAnswerIsTrue, trueFalseCard.TrueLabel, trueFalseCard.FalseLabel, null, null),
            _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
        };
    }

    // --- True/False ---

    private static TrueFalseFlashCard BuildTrueFalseCard(string front, string back, string? answerPayload, ulong id) =>
        BuildTrueFalseCard(front, back, ParseTrueFalsePayload(answerPayload), id);

    private static TrueFalseFlashCard BuildTrueFalseCard(string front, string back,
        (bool correctAnswerIsTrue, string trueLabel, string falseLabel) settings, ulong id) =>
        new(front, back, settings.correctAnswerIsTrue, settings.trueLabel, settings.falseLabel, id);

    private static (bool correctAnswerIsTrue, string trueLabel, string falseLabel) ParseTrueFalsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return (true, "True", "False");
        if (bool.TryParse(payload, out var boolAnswer)) return (boolAnswer, "True", "False");

        try
        {
            var parsed = JsonSerializer.Deserialize<TrueFalseAnswerPayload>(payload);
            return parsed is null
                ? (true, "True", "False")
                : NormaliseTrueFalse(parsed.CorrectAnswerIsTrue, parsed.TrueLabel, parsed.FalseLabel);
        }
        catch (JsonException)
        {
            return (true, "True", "False");
        }
    }

    /// <summary> Blank labels fall back to True/False, and so do labels that match each other. </summary>
    private static (bool correctAnswerIsTrue, string trueLabel, string falseLabel) NormaliseTrueFalse(bool correctAnswerIsTrue, string? trueLabel, string? falseLabel)
    {
        trueLabel = string.IsNullOrWhiteSpace(trueLabel) ? "True" : trueLabel.Trim();
        falseLabel = string.IsNullOrWhiteSpace(falseLabel) ? "False" : falseLabel.Trim();

        if (string.Equals(trueLabel, falseLabel, StringComparison.OrdinalIgnoreCase))
        {
            return (correctAnswerIsTrue, "True", "False");
        }

        return (correctAnswerIsTrue, trueLabel, falseLabel);
    }
}
