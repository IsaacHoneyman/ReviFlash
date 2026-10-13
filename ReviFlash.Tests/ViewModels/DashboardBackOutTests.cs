using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class DashboardBackOutTests
{
    [Fact]
    public void At_the_main_menu_there_is_nothing_to_back_out_of()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();

        Assert.False(vm.BackOut());
    }

    [Fact]
    public void Each_escape_undoes_one_step_search_then_folders_then_nothing()
    {
        using var database = new TestDatabase();
        var outer = FolderRepository.CreateFolder("Outer", null);
        var inner = FolderRepository.CreateFolder("Inner", outer.ID);
        var vm = new DashboardViewModel();
        vm.NavigateToFolder(inner.ID);
        vm.SearchText = "anything";

        Assert.True(vm.BackOut());
        Assert.Equal("", vm.SearchText);
        Assert.Equal(inner.ID, vm.CurrentFolderID);

        Assert.True(vm.BackOut());
        Assert.Equal(outer.ID, vm.CurrentFolderID);

        Assert.True(vm.BackOut());
        Assert.Null(vm.CurrentFolderID);

        Assert.False(vm.BackOut());
    }

    [Fact]
    public void Escape_stops_picking_sets_before_leaving_the_folder()
    {
        using var database = new TestDatabase();
        var folder = FolderRepository.CreateFolder("Folder", null);
        var vm = new DashboardViewModel();
        vm.NavigateToFolder(folder.ID);
        vm.BeginReviewSelection();

        Assert.True(vm.BackOut());

        Assert.False(vm.IsSelectionModeActive);
        Assert.Equal(folder.ID, vm.CurrentFolderID);
    }

    [Fact]
    public void Escape_closes_the_graph_then_set_stats_before_the_search()
    {
        using var database = new TestDatabase();
        var deck = new FlashCardDeck("Set");
        FlashCardRepository.SaveNewDeck(deck, null);
        var vm = new DashboardViewModel();
        vm.SearchText = "Set";
        vm.ShowDeckStats(vm.Decks.Single());
        vm.EnterGraphView();

        Assert.True(vm.BackOut());
        Assert.False(vm.IsGraphView);
        Assert.True(vm.IsViewingStats);

        Assert.True(vm.BackOut());
        Assert.False(vm.IsViewingStats);
        Assert.Equal("Set", vm.SearchText);

        Assert.True(vm.BackOut());
        Assert.Equal("", vm.SearchText);
    }
}
