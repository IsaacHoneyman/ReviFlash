using System.Collections.Generic;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary>
/// Text with \C{...} blanks (Front) and an optional Extra shown with the answer (Back).
/// It isn't reviewed itself: each blank group becomes a flip question, or a typed one with <see cref="TypeAnswer"/>.
/// </summary>
public class ClozeFlashCard : FlashCard
{
    public ClozeFlashCard(string text, string extra, bool typeAnswer) : base(text, extra) { TypeAnswer = typeAnswer; }
    public ClozeFlashCard(string text, string extra, bool typeAnswer, ulong id) : this(text, extra, typeAnswer) { ID = id; }

    /// <summary> The hidden text is typed in, rather than revealed and self-marked. </summary>
    public bool TypeAnswer { get; }

    /// <summary> One question per blank group, sharing this card's ID so stats still go to its deck. </summary>
    public IEnumerable<FlashCard> CreateQuestions()
    {
        foreach (var group in ClozeUtility.Groups(Front))
        {
            var question = ClozeUtility.Mask(Front, group);
            var answer = ClozeUtility.Reveal(Front, group);
            if (!string.IsNullOrWhiteSpace(Back)) answer += "\n\n" + Back;

            yield return TypeAnswer
                ? new TypeFlashCard(question, answer, ClozeUtility.Answer(Front, group), ID)
                : new FlipFlashCard(question, answer, ID);
        }
    }

    /// <summary> Answers are checked on the questions <see cref="CreateQuestions"/> makes. </summary>
    public override bool VerifyAnswer(object answer) => true;
}
