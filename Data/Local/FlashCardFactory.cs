using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Media;
using HarfBuzzSharp;
using ReviFlash.Models;

namespace ReviFlash.Data.Local;

public static class FlashCardFactory
{
    public static FlashCard CreateCard(string cardType, string front, string back, string? answer, ulong id,
    List<(string optionText, bool isCorrect)> options, List<(string leftText, string rightText)> pairs)
    {
        return cardType switch
        {
            nameof(TypeFlashCard) => new TypeFlashCard(front, back, answer, id),
            nameof(FlipFlashCard) => new FlipFlashCard(front, back, id),
            nameof(MultiFlashCard) => new MultiFlashCard(front, back, options, id),
            nameof(MatchFlashCard) => new MatchFlashCard(front, back, pairs, id),
            nameof(TrueFalseFlashCard) => BuildTrueFalseCard(front, back, answer, id),
            _ => throw new InvalidOperationException($"Unknown card type: {cardType}")
        };
    }

    public static object BuildAnswerPayload(FlashCard card)
    {
        return card switch
        {
            TypeFlashCard typeCard => typeCard.Answer ?? (object)DBNull.Value,
            TrueFalseFlashCard trueFalseCard => JsonSerializer.Serialize(
                new TrueFalseAnswerPayload(trueFalseCard.CorrectAnswerIsTrue, trueFalseCard.TrueLabel, trueFalseCard.FalseLabel)),
            _ => DBNull.Value,
        };
    }

    private static TrueFalseFlashCard BuildTrueFalseCard(string front, string back, string? answerPayload, ulong id)
    {
        var (correctAnswerIsTrue, trueLabel, falseLabel) = ParseTrueFalsePayload(answerPayload);
        return new TrueFalseFlashCard(front, back, correctAnswerIsTrue, trueLabel, falseLabel, id);
    }

    private static (bool correctAnswerIsTrue, string trueLabel, string falseLabel) ParseTrueFalsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return (true, "True", "False");
        if (bool.TryParse(payload, out var boolAnswer)) return (boolAnswer, "True", "False");

        try
        {
            var parsed = JsonSerializer.Deserialize<TrueFalseAnswerPayload>(payload);
            if (parsed is null)
            {
                return (true, "True", "False");
            }

            var trueLabel = string.IsNullOrWhiteSpace(parsed.TrueLabel) ? "True" : parsed.TrueLabel.Trim();
            var falseLabel = string.IsNullOrWhiteSpace(parsed.FalseLabel) ? "False" : parsed.FalseLabel.Trim();

            if (string.Equals(trueLabel, falseLabel, StringComparison.OrdinalIgnoreCase))
            {
                return (parsed.CorrectAnswerIsTrue, "True", "False");
            }

            return (parsed.CorrectAnswerIsTrue, trueLabel, falseLabel);
        }
        catch (JsonException)
        {
            return (true, "True", "False");
        }
    }
}
