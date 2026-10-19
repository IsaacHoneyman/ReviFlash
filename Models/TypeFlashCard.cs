using System;
using System.Collections.Generic;
using System.Linq;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

public class TypeFlashCard(string front, string back, string? answer) : FlashCard(front, back)
{
    public override string TypeName => CardUtility.CARD_TYPE_TYPE;

    public string Answer { get; private set; } = NormalizeAnswer(answer, back);
    public TypeFlashCard(string front, string back) : this(front, back, null) { }
    public TypeFlashCard(string front, string back, ulong id) : this(front, back, null, id) { }
    public TypeFlashCard(string front, string back, string? answer, ulong id) : this(front, back, answer) { ID = id; }

    public void UpdateAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer cannot be empty.", nameof(answer));

        Answer = answer.Trim();
    }

    /// <summary> The text of each blank when this asks for several at once; they can be typed in order with commas or spaces between. </summary>
    public IReadOnlyList<string>? BlankAnswers { get; init; }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not string strAnswer) return false;
        if (BlankAnswers is { Count: > 1 }) return SameWords(strAnswer, string.Join(' ', BlankAnswers));
        return CollapseSpaces(strAnswer).Equals(CollapseSpaces(Answer), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameWords(string typed, string expected) =>
        Words(typed).SequenceEqual(Words(expected), StringComparer.OrdinalIgnoreCase);

    private static string CollapseSpaces(string text) => string.Join(' ', text.Normalize().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string[] Words(string text) => text.Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

    private static string NormalizeAnswer(string? answer, string fallbackBack)
    {
        var value = string.IsNullOrWhiteSpace(answer) ? fallbackBack : answer;
        return value.Trim();
    }
}