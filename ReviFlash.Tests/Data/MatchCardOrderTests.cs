using ReviFlash.Data.Local;
using ReviFlash.Models;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class MatchCardOrderTests
{
    private static readonly List<(string leftText, string rightText)> Pairs = [("a", "1"), ("b", "2"), ("c", "3")];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Keeping_the_left_order_survives_saving_and_loading(bool keepLeftOrder)
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Set");
        FlashCardRepository.SaveNewDeck(deck);
        FlashCardRepository.SaveNewCard(new MatchFlashCard("Q", "Q", Pairs, keepLeftOrder), deck.ID);

        var loaded = Assert.IsType<MatchFlashCard>(Assert.Single(FlashCardRepository.GetCardsForDeck(deck.ID)));

        Assert.Equal(keepLeftOrder, loaded.KeepLeftOrder);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Keeping_the_left_order_survives_export_and_import(bool keepLeftOrder)
    {
        var entry = FlashCardFactory.ToExportEntry(new MatchFlashCard("Q", "Q", Pairs, keepLeftOrder));

        var imported = Assert.IsType<MatchFlashCard>(FlashCardFactory.FromExportEntry(entry));

        Assert.Equal(keepLeftOrder, imported.KeepLeftOrder);
        Assert.Equal(Pairs, imported.Options);
    }
}
