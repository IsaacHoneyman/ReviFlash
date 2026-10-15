using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class MoveCardViewModelTests
{
    private static FlashCardDeck Deck(ulong id, string name)
    {
        var deck = new FlashCardDeck(name);
        deck.AssignDatabaseID(id);
        return deck;
    }

    private static List<string> Offered(MoveCardViewModel vm) =>
        [.. vm.Browser.Items.OfType<FlashCardDeck>().Select(deck => deck.Name).Order()];

    [Fact]
    public void Moving_offers_every_set_but_the_cards_own_and_copying_offers_them_all()
    {
        using var database = new TestDatabase();
        var vm = new MoveCardViewModel([Deck(1, "Own"), Deck(2, "Other")], currentDeckID: 1);

        Assert.Equal(["Other"], Offered(vm));
        Assert.Equal("Move", vm.Browser.ActionText);

        vm.IsCopySelected = true;

        Assert.Equal(["Other", "Own"], Offered(vm));
        Assert.Equal("Copy", vm.Browser.ActionText);
        Assert.False(vm.IsMove);
    }

    [Fact]
    public void Unticking_the_selected_choice_keeps_it_selected()
    {
        using var database = new TestDatabase();
        var vm = new MoveCardViewModel([Deck(1, "Own"), Deck(2, "Other")], currentDeckID: 1);

        vm.IsMove = false;

        Assert.True(vm.IsMove);
        Assert.False(vm.IsCopy);
    }
}
