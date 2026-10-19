using ReviFlash.Models;
using ReviFlash.Utilities;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

public class SummaryViewModelTests
{
    [Fact]
    public void The_grade_matches_the_percentage_shown()
    {
        var summary = new SummaryViewModel(359, 399, TimeSpan.FromMinutes(1));

        Assert.Equal(90, summary.Percentage);
        Assert.Equal("A*", summary.Grade);
    }

    [Fact]
    public void Nothing_answered_is_ungraded()
    {
        var summary = new SummaryViewModel(0, 0, TimeSpan.Zero);

        Assert.Equal(0, summary.Percentage);
        Assert.Equal("-", summary.Grade);
    }

    [Fact]
    public void A_set_grade_matches_its_shown_percentage_too()
    {
        Assert.Equal(90, CardUtility.AccuracyPercent(359, 399));
        Assert.Equal("A*", CardUtility.CalculateGradeWithDefault(359, 399));
    }

    [Theory]
    [InlineData(1, "1 card")]
    [InlineData(2, "2 cards")]
    [InlineData(0, "0 cards")]
    public void A_set_counts_its_cards_in_words(int count, string expected)
    {
        var deck = new FlashCardDeck("Set", 1, count);

        Assert.Equal(expected, deck.CardCountText);
    }

    [Fact]
    public void Card_badges_say_one_in_the_singular()
    {
        Assert.Equal("Card", new FlashCardDeck("Set", 1, 1).CardCountLabel);
        Assert.Equal("Cards", new FlashCardDeck("Set", 1, 2).CardCountLabel);
        Assert.Equal("Set", new StudyGroup("Group", 1, 1, 5).DeckCountLabel);
        Assert.Equal("Sets", new StudyGroup("Group", 1, 0, 0).DeckCountLabel);
        Assert.Equal("Item", new Folder("Folder") { SetCount = 1 }.ItemCountLabel);
        Assert.Equal("Items", new Folder("Folder") { SetCount = 1, NoteCount = 1 }.ItemCountLabel);
    }
}
