using System.Collections.Generic;
using ReviFlash.Utilities;

namespace ReviFlash.Models;

/// <summary> Text with \C{...} blanks (Front) and an optional Extra (Back); each blank group is reviewed as its own question. </summary>
public class ClozeFlashCard : FlashCard
{
    public ClozeFlashCard(string text, string extra, bool typeAnswer) : base(text, extra) { TypeAnswer = typeAnswer; }
    public ClozeFlashCard(string text, string extra, bool typeAnswer, ulong id) : this(text, extra, typeAnswer) { ID = id; }

    /// <summary> Blanks are answered by typing; otherwise they are revealed and self-marked. </summary>
    public bool TypeAnswer { get; }

    /// <summary> One question per blank group, sharing this card's ID so stats go to its deck. </summary>
    public IEnumerable<FlashCard> CreateQuestions()
    {
        foreach (var group in ClozeUtility.Groups(Front))
        {
            var question = ClozeUtility.Mask(Front, group);
            var answer = ClozeUtility.Reveal(Front, group);
            if (!string.IsNullOrWhiteSpace(Back)) answer += "\n\n" + Back;

            yield return TypeAnswer
                ? new TypeFlashCard(question, answer, ClozeUtility.Answer(Front, group), ID) { ReviewLabel = CardUtility.CARD_TYPE_CLOZE }
                : new FlipFlashCard(question, answer, ID) { ReviewLabel = CardUtility.CARD_TYPE_CLOZE };
        }
    }

    /// <summary> Answers are checked on the questions <see cref="CreateQuestions"/> makes. </summary>
    public override bool VerifyAnswer(object answer) => true;
}
