namespace ReviFlash.Models;

public class FlipFlashCard : FlashCard
{
    public FlipFlashCard(string front, string back, bool isReversible = false) : base(front, back) { IsReversible = isReversible; }
    public FlipFlashCard(string front, string back, ulong id, bool isReversible = false) : this(front, back, isReversible) { ID = id; }

    /// <summary> Also asked back to front in reviews, as a separate question. </summary>
    public bool IsReversible { get; }

    /// <summary> The back-to-front question a review session made from a reversible card. </summary>
    public bool IsReversedCopy { get; private init; }

    /// <summary> Keeps the card ID so stats go to the card's deck. </summary>
    public FlipFlashCard CreateReversedCopy() => new(Back, Front, ID) { IsReversedCopy = true };

    public override bool VerifyAnswer(object answer) { return true; }
}
