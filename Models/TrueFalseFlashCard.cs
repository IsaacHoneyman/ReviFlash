using System;

namespace ReviFlash.Models;

public class TrueFalseFlashCard : FlashCard
{
    public bool CorrectAnswerIsTrue { get; private set; }
    public string TrueLabel { get; private set; }
    public string FalseLabel { get; private set; }

    public TrueFalseFlashCard(string front, string back, bool correctAnswerIsTrue)
        : this(front, back, correctAnswerIsTrue, "True", "False")
    {
    }

    public TrueFalseFlashCard(string front, string back, bool correctAnswerIsTrue, string trueLabel, string falseLabel)
        : base(front, back)
    {
        CorrectAnswerIsTrue = correctAnswerIsTrue;
        (TrueLabel, FalseLabel) = NormalizeLabels(trueLabel, falseLabel);
    }

    public TrueFalseFlashCard(string front, string back, bool correctAnswerIsTrue, ulong id)
        : this(front, back, correctAnswerIsTrue, "True", "False")
    {
        ID = id;
    }

    public TrueFalseFlashCard(string front, string back, bool correctAnswerIsTrue, string trueLabel, string falseLabel, ulong id)
        : this(front, back, correctAnswerIsTrue, trueLabel, falseLabel)
    {
        ID = id;
    }

    public void UpdateTrueFalseSettings(bool correctAnswerIsTrue, string trueLabel, string falseLabel)
    {
        CorrectAnswerIsTrue = correctAnswerIsTrue;
        (TrueLabel, FalseLabel) = NormalizeLabels(trueLabel, falseLabel);
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is bool boolAnswer)
        {
            return boolAnswer == CorrectAnswerIsTrue;
        }

        if (answer is not string stringAnswer)
        {
            return false;
        }

        var normalized = stringAnswer.Trim();
        if (normalized.Equals(TrueLabel, StringComparison.OrdinalIgnoreCase))
        {
            return CorrectAnswerIsTrue;
        }

        if (normalized.Equals(FalseLabel, StringComparison.OrdinalIgnoreCase))
        {
            return !CorrectAnswerIsTrue;
        }

        if (bool.TryParse(normalized, out var parsedBool))
        {
            return parsedBool == CorrectAnswerIsTrue;
        }

        return false;
    }

    private static (string trueLabel, string falseLabel) NormalizeLabels(string trueLabel, string falseLabel)
    {
        var normalizedTrue = string.IsNullOrWhiteSpace(trueLabel) ? "True" : trueLabel.Trim();
        var normalizedFalse = string.IsNullOrWhiteSpace(falseLabel) ? "False" : falseLabel.Trim();

        if (normalizedTrue.Equals(normalizedFalse, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("True and False labels must be different.");
        }

        return (normalizedTrue, normalizedFalse);
    }
}