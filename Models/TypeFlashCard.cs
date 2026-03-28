using System;

namespace ReviFlash.Models;

public class TypeFlashCard : FlashCard
{
    public string Answer { get; private set; }

    public TypeFlashCard(string front, string back) : this(front, back, null) { }

    public TypeFlashCard(string front, string back, string? answer) : base(front, back)
    {
        Answer = NormalizeAnswer(answer, back);
    }

    public TypeFlashCard(string front, string back, ulong id) : this(front, back, null, id) { }

    public TypeFlashCard(string front, string back, string? answer, ulong id) : this(front, back, answer)
    {
        ID = id;
    }

    public void UpdateAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ArgumentException("Answer cannot be empty.", nameof(answer));
        }

        Answer = answer.Trim();
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not string strAnswer) return false;
        return strAnswer.Trim().Equals(Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAnswer(string? answer, string fallbackBack)
    {
        var value = string.IsNullOrWhiteSpace(answer) ? fallbackBack : answer;
        return value.Trim();
    }
}