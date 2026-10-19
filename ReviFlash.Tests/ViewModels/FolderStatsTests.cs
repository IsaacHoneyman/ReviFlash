using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class FolderStatsTests
{
    private static FlashCardDeck NewDeck(string name, ulong? folderID)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck, folderID);
        return deck;
    }

    /// <summary> Outer holds a set and a note, Inner (inside Outer) holds a set, and one set sits outside both. </summary>
    private static (Folder outer, Folder inner) NewLibrary()
    {
        var outer = FolderRepository.CreateFolder("Outer", null);
        var inner = FolderRepository.CreateFolder("Inner", outer.ID);

        var outerDeck = NewDeck("Outer set", outer.ID);
        var innerDeck = NewDeck("Inner set", inner.ID);
        var outsideDeck = NewDeck("Outside set", null);
        var note = NoteRepository.CreateNote("Outer note", outer.ID);

        FlashCardRepository.UpdateDeckStats([(outerDeck.ID, 3, 4, 40), (innerDeck.ID, 1, 2, 20), (outsideDeck.ID, 9, 9, 900)]);
        FlashCardRepository.AddEditingTime(innerDeck.ID, 15);
        FlashCardRepository.AddEditingTime(outsideDeck.ID, 500);
        NoteRepository.AddStudyTime(note.ID, 30);

        return (outer, inner);
    }

    [Fact]
    public void A_folders_stats_cover_everything_in_it_and_its_subfolders_only()
    {
        using var database = new TestDatabase();
        var (outer, _) = NewLibrary();
        var vm = new DashboardViewModel();

        vm.ShowFolderStats(outer.ID);

        Assert.Equal(6, vm.TotalQuestions);
        Assert.Equal(4, vm.TotalCorrect);
        Assert.Equal(60, vm.TotalTimeSeconds);
        Assert.Equal(15, vm.EditingTimeSeconds);
        Assert.Equal(30, vm.NoteTimeSeconds);
        Assert.Equal("Outer", vm.CurrentStatsTitle);
    }

    [Fact]
    public void A_subfolders_stats_leave_out_its_parent()
    {
        using var database = new TestDatabase();
        var (_, inner) = NewLibrary();
        var vm = new DashboardViewModel();

        vm.ShowFolderStats(inner.ID);

        Assert.Equal(2, vm.TotalQuestions);
        Assert.Equal(20, vm.TotalTimeSeconds);
        Assert.Equal(0, vm.NoteTimeSeconds);
    }

    [Fact]
    public void A_folders_stats_split_the_time_like_overall()
    {
        using var database = new TestDatabase();
        var (outer, _) = NewLibrary();
        var vm = new DashboardViewModel();

        vm.ShowFolderStats(outer.ID);

        Assert.True(vm.ShowTimeSplit);
        Assert.True(vm.ShowCardStats);
        Assert.False(vm.IsViewingCardStats);
    }

    [Fact]
    public void Where_the_time_went_lists_only_the_folders_sets_and_notes()
    {
        using var database = new TestDatabase();
        var (outer, _) = NewLibrary();
        var vm = new DashboardViewModel();

        vm.ShowFolderStats(outer.ID);

        Assert.Equal(["Inner set", "Outer set", "Outer note"], vm.TimeBreakdown.Select(item => item.Name).OrderBy(name => name.Contains("note")).ThenBy(name => name));
    }

    [Fact]
    public void Deleting_the_folder_being_viewed_goes_back_to_overall_stats()
    {
        using var database = new TestDatabase();
        var (outer, _) = NewLibrary();
        var vm = new DashboardViewModel();
        vm.ShowFolderStats(outer.ID);

        vm.DeleteFolder(vm.FolderTree.AllFolders.Single(f => f.ID == outer.ID), withContents: true);

        Assert.False(vm.IsViewingStats);
        Assert.Equal(900, vm.TotalTimeSeconds);
    }

    [Fact]
    public void Deleting_the_folder_being_viewed_but_keeping_its_contents_goes_back_to_overall_stats()
    {
        using var database = new TestDatabase();
        var (outer, _) = NewLibrary();
        var vm = new DashboardViewModel();
        vm.ShowFolderStats(outer.ID);

        vm.DeleteFolder(vm.FolderTree.AllFolders.Single(f => f.ID == outer.ID));

        Assert.False(vm.IsViewingStats);
        Assert.Equal("Overall", vm.CurrentStatsTitle);
    }
}
