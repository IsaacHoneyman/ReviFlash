using System;

namespace ReviFlash.Models;

public class TypeFlashCard : FlashCard
{
    public TypeFlashCard(string front, string back) : base(front, back) { }

    public TypeFlashCard(string front, string back, ulong id) : base(front, back)
    {
        ID = id;
    }

    public override bool VerifyAnswer(object answer)
    {
        if (answer is not string strAnswer) return false;
        return strAnswer.Trim().Equals(Back.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}