using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class DashboardViewModelTests
{
    private static FlashCardDeck NewDeck(string name, ulong? folderID = null)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck, folderID);
        return deck;
    }

    [Fact]
    public void Deleting_a_folder_with_its_contents_drops_its_sets_from_the_selection()
    {
        using var database = new TestDatabase();
        var folder = FolderRepository.CreateFolder("Folder", null);
        var inside = NewDeck("Inside", folder.ID);
        var outside = NewDeck("Outside");
        var vm = new DashboardViewModel();
        vm.BeginReviewSelection();
        vm.ToggleDeckSelection(vm.Decks.Single(d => d.ID == inside.ID));
        vm.ToggleDeckSelection(vm.Decks.Single(d => d.ID == outside.ID));

        vm.DeleteFolder(vm.FolderTree.AllFolders.Single(f => f.ID == folder.ID), withContents: true);

        Assert.Equal(1, vm.SelectedDeckCount);
        Assert.Equal(outside.ID, Assert.Single(vm.GetSelectedDecks()).ID);
    }

    [Fact]
    public void Reloading_after_a_set_vanishes_prunes_the_selection()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Gone");
        var vm = new DashboardViewModel();
        vm.BeginReviewSelection();
        vm.ToggleDeckSelection(vm.Decks.Single());

        FlashCardRepository.DeleteDeck(deck.ID);
        vm.ReloadLibrary();

        Assert.False(vm.HasSelectedDecks);
        Assert.Equal("Cancel", vm.ReviewSelectionButtonText);
    }

    [Fact]
    public void Stats_for_an_edited_set_show_its_new_name_and_card_count()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Old");
        var vm = new DashboardViewModel();
        vm.ShowDeckStats(vm.Decks.Single());

        deck.Name = "New";
        FlashCardRepository.UpdateDeck(deck);
        FlashCardRepository.SaveNewCard(new FlipFlashCard("q", "a"), deck.ID);
        vm.ReloadAfterEdit();

        Assert.Equal("New", vm.CurrentStatsTitle);
        Assert.Equal(1, vm.TotalCardCount);
    }

    [Fact]
    public void Restoring_a_backup_resets_the_stats_to_overall()
    {
        using var database = new TestDatabase();
        NewDeck("Set");
        var vm = new DashboardViewModel();
        vm.ShowDeckStats(vm.Decks.Single());

        vm.RefreshAfterBackupRestore();

        Assert.Equal(DashboardViewModel.StatsScope.Overall, vm.CurrentStatsScope);
        Assert.Equal("Overall", vm.CurrentStatsTitle);
    }
}
