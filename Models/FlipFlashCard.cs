namespace ReviFlash.Models;

public class FlipFlashCard : FlashCard
{
    public FlipFlashCard(string front, string back) : base(front, back) { }

    public FlipFlashCard(string front, string back, ulong id) : base(front, back)
    {
        ID = id;
    }

    public override bool VerifyAnswer(object answer)
    {
        return true;
    }
}