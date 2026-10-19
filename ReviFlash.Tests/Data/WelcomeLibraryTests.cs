using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.Utilities;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class WelcomeLibraryTests
{
    private const string Logo = "0123456789abcdef0123456789abcdef.webp";

    [Fact]
    public void A_fresh_library_gets_the_welcome_folder_with_a_tour_an_example_set_a_group_and_a_read_me()
    {
        using var database = new TestDatabase();

        WelcomeLibrary.Create(Logo);

        var folders = FolderRepository.GetAllFolders();
        var welcome = Assert.Single(folders, f => f.ParentFolderID is null);
        Assert.Equal(WelcomeLibrary.FolderName, welcome.Name);
        var exampleFolder = Assert.Single(folders, f => f.ParentFolderID == welcome.ID);
        Assert.Equal("Example Folder", exampleFolder.Name);

        var decks = FlashCardRepository.GetAllDecks();
        var tour = Assert.Single(decks, d => d.Name == "Feature Tour");
        var example = Assert.Single(decks, d => d.Name == "Example Set");
        Assert.Equal((welcome.ID, exampleFolder.ID), (tour.FolderID, example.FolderID));
        Assert.Single(FlashCardRepository.GetCardsForDeck(example.ID));

        var group = Assert.Single(FlashCardRepository.GetAllStudyGroups());
        Assert.Equal(("Example Group", welcome.ID), (group.Name, group.FolderID));
        Assert.Equal(["Example Set", "Feature Tour"], FlashCardRepository.GetDecksForStudyGroup(group.ID).Select(d => d.Name).Order());

        var note = Assert.Single(NoteRepository.GetAllNotes());
        Assert.Equal(("Read Me", welcome.ID), (note.Name, note.FolderID));
        Assert.StartsWith("\\H1{Welcome To ReviFlash}", NoteRepository.GetContent(note.ID));
    }

    [Fact]
    public void The_tour_has_every_card_type_and_shows_the_logo()
    {
        using var database = new TestDatabase();

        WelcomeLibrary.Create(Logo);

        var cards = TourCards();
        Assert.Equal(CardUtility.CARD_TYPE_MATCH_PLACEHOLDER, cards.OfType<MatchFlashCard>().Single().Front);
        Assert.Equal(6, cards.Select(card => card.GetType()).Distinct().Count());
        Assert.Contains(cards, card => card is FlipFlashCard { IsReversible: true });
        Assert.Contains(cards, card => card is ClozeFlashCard { TypeAnswer: true });
        Assert.Contains(cards, card => card.Front.Contains(ImageToken.Format(Logo, 25)));
    }

    [Fact]
    public void Without_a_logo_the_image_card_is_left_out()
    {
        using var database = new TestDatabase();

        WelcomeLibrary.Create(null);

        Assert.DoesNotContain(TourCards(), card => card.Front.Contains("\\img"));
    }

    [Fact]
    public void Every_formula_in_the_welcome_folder_typesets()
    {
        using var database = new TestDatabase();
        WelcomeLibrary.Create(Logo);

        var texts = new List<string> { NoteRepository.GetContent(NoteRepository.GetAllNotes().Single().ID) };
        foreach (var card in FlashCardRepository.GetAllDecks().SelectMany(deck => FlashCardRepository.GetCardsForDeck(deck.ID)))
        {
            texts.Add(card.Back);
            if (card is ClozeFlashCard) texts.AddRange(ClozeUtility.Groups(card.Front).SelectMany(g => new[] { ClozeUtility.Mask(card.Front, g), ClozeUtility.Reveal(card.Front, g) }));
            else texts.Add(card.Front);
            if (card is MultiFlashCard multi) texts.AddRange(multi.Options.Select(o => o.optionText));
            if (card is MatchFlashCard match) texts.AddRange(match.Options.SelectMany(o => new[] { o.leftText, o.rightText }));
        }

        var maths = texts.SelectMany(text => LatexUtility.Parse(text)).OfType<MathSegment>().ToList();
        Assert.NotEmpty(maths);
        foreach (var segment in maths)
        {
            var painter = new CSharpMath.Avalonia.MathPainter { LaTeX = segment.Latex };
            Assert.True(painter.ErrorMessage is null, $"{segment.Source}: {painter.ErrorMessage}");
        }
    }

    private static List<FlashCard> TourCards() =>
        FlashCardRepository.GetCardsForDeck(FlashCardRepository.GetAllDecks().Single(d => d.Name == "Feature Tour").ID);
}
