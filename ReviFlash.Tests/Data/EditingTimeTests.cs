using ReviFlash.Data.Local;
using ReviFlash.Models;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class EditingTimeTests
{
    private static FlashCardDeck NewDeck(string name, ulong? folderID = null)
    {
        var deck = new FlashCardDeck(name);
        FlashCardRepository.SaveNewDeck(deck, folderID);
        return deck;
    }

    [Fact]
    public void Editing_time_on_the_same_day_adds_up()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Set");

        FlashCardRepository.AddEditingTime(deck.ID, 60);
        FlashCardRepository.AddEditingTime(deck.ID, 30);

        Assert.Equal(90, FlashCardRepository.GetEditingSeconds(deck.ID, null));
        var day = Assert.Single(FlashCardRepository.GetEditingTimeByDate(deck.ID, null));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), day.date);
    }

    [Fact]
    public void Editing_time_is_kept_per_set_and_totals_across_sets_and_groups()
    {
        using var database = new TestDatabase();
        var first = NewDeck("First");
        var second = NewDeck("Second");
        var group = new StudyGroup("Group");
        FlashCardRepository.SaveNewStudyGroup(group, [first.ID]);

        FlashCardRepository.AddEditingTime(first.ID, 40);
        FlashCardRepository.AddEditingTime(second.ID, 20);

        Assert.Equal(40, FlashCardRepository.GetEditingSeconds(first.ID, null));
        Assert.Equal(60, FlashCardRepository.GetEditingSeconds(null, null));
        Assert.Equal(40, FlashCardRepository.GetStudyGroupEditingSeconds(group.ID, null));
    }

    [Fact]
    public void Deleting_a_set_or_its_stats_removes_its_editing_time()
    {
        using var database = new TestDatabase();
        var cleared = NewDeck("Cleared");
        var deleted = NewDeck("Deleted");
        FlashCardRepository.AddEditingTime(cleared.ID, 30);
        FlashCardRepository.AddEditingTime(deleted.ID, 30);

        FlashCardRepository.DeleteStatsForDeck(cleared.ID);
        FlashCardRepository.DeleteDeck(deleted.ID);

        Assert.Equal(0, Db.Scalar<int>("SELECT COUNT(*) FROM DeckEditStats;"));
    }

    [Fact]
    public void Deleting_all_stats_removes_editing_time()
    {
        using var database = new TestDatabase();
        FlashCardRepository.AddEditingTime(NewDeck("Set").ID, 30);

        FlashCardRepository.DeleteAllStats();

        Assert.Equal(0, FlashCardRepository.GetEditingSeconds(null, null));
    }

    [Fact]
    public void Deleting_a_folder_with_its_contents_removes_its_sets_editing_time()
    {
        using var database = new TestDatabase();
        var folder = FolderRepository.CreateFolder("Folder", null);
        FlashCardRepository.AddEditingTime(NewDeck("Inside", folder.ID).ID, 30);

        FolderRepository.DeleteFolderAndContents([folder.ID]);

        Assert.Equal(0, Db.Scalar<int>("SELECT COUNT(*) FROM DeckEditStats;"));
    }

    [Fact]
    public void Total_time_adds_reviewing_notes_and_editing()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Set");
        FlashCardRepository.UpdateDeckStats([(deck.ID, 1, 2, 100)]);
        NoteRepository.AddStudyTime(NoteRepository.CreateNote("Note", null).ID, 50);
        FlashCardRepository.AddEditingTime(deck.ID, 25);

        var vm = new DashboardViewModel();

        Assert.Equal(100, vm.TotalTimeSeconds);
        Assert.Equal(50, vm.NoteTimeSeconds);
        Assert.Equal(25, vm.EditingTimeSeconds);
        Assert.Equal(175, vm.AllTimeSeconds);
    }

    [Fact]
    public void A_sets_stats_show_its_own_editing_time()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Set");
        FlashCardRepository.AddEditingTime(deck.ID, 25);
        FlashCardRepository.AddEditingTime(NewDeck("Other").ID, 99);
        var vm = new DashboardViewModel();

        vm.ShowDeckStats(vm.Decks.Single(d => d.ID == deck.ID));

        Assert.Equal(25, vm.EditingTimeSeconds);
    }

    [Fact]
    public void Editing_time_shows_in_the_study_time_chart()
    {
        using var database = new TestDatabase();
        FlashCardRepository.AddEditingTime(NewDeck("Set").ID, 25);
        var vm = new DashboardViewModel();

        var day = Assert.Single(vm.TimeGraphPoints);

        Assert.Equal(25, day.EditingSeconds);
        Assert.Equal(25, day.TotalSeconds);
    }

    [Fact]
    public void Make_card_time_goes_to_the_set_the_card_was_added_to()
    {
        using var database = new TestDatabase();
        var deck = NewDeck("Target");
        var vm = new MakeCardViewModel("Back", "Front");
        vm.SelectedDeck = vm.DeckBrowser.Items.OfType<FlashCardDeck>().Single();

        vm.EditingTime.Tick();
        Assert.NotNull(vm.TrySave());
        vm.EditingTime.Stop();

        Assert.Equal(5, FlashCardRepository.GetEditingSeconds(deck.ID, null));
    }

    [Fact]
    public void Make_card_time_is_dropped_when_no_card_is_added()
    {
        using var database = new TestDatabase();
        NewDeck("Target");
        var vm = new MakeCardViewModel("Back", "Front");

        vm.EditingTime.Tick();
        vm.EditingTime.Stop();

        Assert.Equal(0, FlashCardRepository.GetEditingSeconds(null, null));
    }

    [Fact]
    public void The_chart_tooltip_names_only_the_kinds_of_time_spent()
    {
        Assert.Equal("Mar 3: 1:40 spent", GraphStatPointViewModel.TimeTooltipFor("Mar 3", 100, 0, 0));
        Assert.Equal("Mar 3: 2:05 spent (1:40 reviewing, 0:25 editing)", GraphStatPointViewModel.TimeTooltipFor("Mar 3", 100, 0, 25));
    }
}
