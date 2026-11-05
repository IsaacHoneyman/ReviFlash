using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class PageHistoryTests
{
    [Fact]
    public void Going_back_returns_to_the_page_underneath_as_it_was()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();
        var review = new object();
        var editor = new object();

        vm.CurrentPage = review;
        vm.OpenPage(editor);
        Assert.Same(editor, vm.CurrentPage);

        Assert.True(vm.GoBack());
        Assert.Same(review, vm.CurrentPage);
    }

    [Fact]
    public void Pages_opened_from_the_main_menu_go_back_to_it()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();
        var settings = new object();
        var themes = new object();

        vm.OpenPage(settings);
        vm.OpenPage(themes);

        Assert.True(vm.GoBack());
        Assert.Same(settings, vm.CurrentPage);
        Assert.True(vm.GoBack());
        Assert.Same(vm, vm.CurrentPage);
        Assert.False(vm.GoBack());
    }

    [Fact]
    public void Returning_to_the_main_menu_forgets_the_pages_that_were_open()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();

        vm.CurrentPage = new object();
        vm.OpenPage(new object());
        vm.CurrentPage = vm;

        Assert.False(vm.GoBack());
        Assert.Same(vm, vm.CurrentPage);
    }
}
