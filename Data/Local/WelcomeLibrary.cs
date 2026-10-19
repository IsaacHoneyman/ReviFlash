using System.Collections.Generic;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Data.Local;

/// <summary> The Welcome! folder a fresh install starts with: a tour of every card type, a one-card set in a subfolder, a group of both, and a Read Me. </summary>
public static class WelcomeLibrary
{
    public const string FolderName = "Welcome!";

    /// <param name="logoImage"> A stored image of the ReviFlash logo for the image card; that card is left out without one. </param>
    public static void Create(string? logoImage)
    {
        var welcome = FolderRepository.CreateFolder(FolderName, null);
        var exampleFolder = FolderRepository.CreateFolder("Example Folder", welcome.ID);

        var tour = new FlashCardDeck("Feature Tour");
        FlashCardRepository.SaveNewDeck(tour, welcome.ID);
        foreach (var card in TourCards(logoImage)) FlashCardRepository.SaveNewCard(card, tour.ID);

        var example = new FlashCardDeck("Example Set");
        FlashCardRepository.SaveNewDeck(example, exampleFolder.ID);
        FlashCardRepository.SaveNewCard(new FlipFlashCard(
            "This set lives in \\B{Example Folder}.",
            "Sets in different folders can still be studied together: \\B{Example Group} has this set and the Feature Tour."), example.ID);

        FlashCardRepository.SaveNewStudyGroup(new StudyGroup("Example Group"), [tour.ID, example.ID], welcome.ID);

        var readMe = NoteRepository.CreateNote("Read Me", welcome.ID);
        NoteRepository.SaveContent(readMe.ID, ReadMe);
    }

    private static List<FlashCard> TourCards(string? logoImage)
    {
        List<FlashCard> cards =
        [
            new FlipFlashCard(
                "\\H2{Welcome to the Feature Tour}\nThis set has one of every card type. Show the answer, then mark whether you got it right.",
                "That was a \\B{Flip} card: a question on the front and the answer on the back.\nText can be \\B{bold}, \\I{italic}, \\U{underlined} or \\T{code}."),
            new FlipFlashCard("The derivative of $\\sin x$", "$\\cos x$", isReversible: true),
            new FlipFlashCard(
                "Expand $(a+b)^2$, showing your working.",
                "$$\\begin{aligned} (a+b)^2 &= (a+b)(a+b) \\\\ &= a^2 + 2ab + b^2 \\end{aligned}$$"),
            new FlipFlashCard(
                "What is the determinant of $\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}$?",
                "$ad - bc$\n- If it's zero, the matrix has no inverse\n- Swapping two rows flips its sign"),
            new ClozeFlashCard(
                "The roots of $ax^2 + bx + c = 0$ are $x = \\C{\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}}$.",
                "Blanks work inside maths too.", typeAnswer: false),
            new ClozeFlashCard(
                "\\C1{Sodium} and \\C1{chlorine} react to make \\C2{sodium chloride}.",
                "Blanks with the same number are hidden together, so this card asks two questions.", typeAnswer: false),
            new ClozeFlashCard("The capital of France is \\C{Paris}.", "This cloze card asks you to type the answer.", typeAnswer: true),
            new TypeFlashCard("Type the chemical symbol for gold.", "\\B{Au}, from the Latin \\I{aurum}.", "Au"),
            new MultiFlashCard(
                "Which of these numbers are prime?",
                "A prime has exactly two factors: 1 and itself.",
                [("2", true), ("9", false), ("11", true), ("15", false)]),
            new MatchFlashCard(CardUtility.CARD_TYPE_MATCH_PLACEHOLDER, "Each constant goes with its first few digits.",
                [("$\\pi$", "3.14159..."), ("$e$", "2.71828..."), ("$\\sqrt{2}$", "1.41421...")]),
            new TrueFalseFlashCard("$\\int_0^1 x\\,dx = \\frac{1}{2}$", "It's the area of a triangle with base 1 and height 1.", correctAnswerIsTrue: true),
            new TrueFalseFlashCard(
                "Are the integers $\\mathbb{Z}$ closed under division?",
                "No: $1 \\div 2 = \\frac{1}{2} \\notin \\mathbb{Z}$.\nTrue/False cards can use your own labels instead of True and False.",
                correctAnswerIsTrue: false, trueLabel: "Closed", falseLabel: "Not closed"),
        ];

        if (logoImage is not null)
        {
            cards.Add(new FlipFlashCard(
                $"Cards and notes can hold images. Which app's logo is this?\n{ImageToken.Format(logoImage, 25)}",
                "\\B{ReviFlash}!\nAdd an image with the \\B{Image} button, by pasting one (Ctrl+V), or by dragging an image file in. A size like [25%] sets its share of the width."));
        }
        return cards;
    }

    private static readonly string ReadMe = string.Join("\n\n",
        "\\H1{Welcome To ReviFlash}\nReviFlash is for exam revision: make flashcards and lecture notes, then study whatever you choose, whenever you like. There are no due dates and no backlog.",
        "\\H2{Getting Around}\n- Click a card on the main menu to open it: a set starts a review, a note opens, and a folder shows what's inside.\n- Use \\B{+ New} to make a set, note, folder or study group.\n- The buttons on each card edit it, show its stats, move it to a folder or delete it.\n- Press \\T{F1} at any time to see every keyboard shortcut.",
        "\\H2{Making Cards}\nOpen a set's editor with its pencil button and pick a card type: Flip, Cloze, Type to Answer, Multiple Choice, Match or True/False. The \\B{Feature Tour} set has one of each.\nWrite maths between dollar signs, like $e^{i\\pi} + 1 = 0$, or between double dollar signs to give it a line of its own. The Formatting Guide in Help lists everything you can use.",
        "\\H2{Studying}\nClick a set to review it. Mark each card right or wrong, use \\B{Retry Later} for any you want to see again, and get a grade at the end.\nTo revise several sets at once, put them in a study group like \\B{Example Group}, or pick them with \\B{Select Multiple}.",
        "\\H2{Notes}\nNotes hold headings, maths and images, just like cards. Click a paragraph to edit it, press \\T{Tab} to show the contents, and press \\T{Ctrl+Enter} to turn what you're writing into a card.",
        "\\H2{Your Stats}\nThe Overall Stats panel shows your accuracy, grade and time spent, and Graph View shows how they change over time. Every set, group, folder and note has its own stats too.",
        "\\H2{Making It Yours}\nIn Settings you can pick a theme or make your own, import decks from Anki, and back up your whole library.");
}
