using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class DialogStackTests
{
    [Fact]
    public async Task A_dialog_finishes_with_the_result_it_closes_with()
    {
        var stack = new DialogStack<object>();
        var dialog = new object();

        var shown = stack.Push(dialog);
        Assert.False(shown.IsCompleted);

        Assert.True(stack.Close(dialog, "Renamed"));
        Assert.Equal("Renamed", await shown);
        Assert.False(stack.IsOpen);
    }

    [Fact]
    public void The_newest_dialog_is_on_top_until_it_closes()
    {
        var stack = new DialogStack<object>();
        var settings = new object();
        var confirm = new object();

        stack.Push(settings);
        stack.Push(confirm);
        Assert.Same(confirm, stack.Top);

        stack.Close(confirm, true);
        Assert.Same(settings, stack.Top);
        Assert.True(stack.IsOpen);
    }

    [Fact]
    public async Task Closing_a_dialog_underneath_leaves_the_top_one_open()
    {
        var stack = new DialogStack<object>();
        var below = new object();
        var top = new object();

        var belowShown = stack.Push(below);
        var topShown = stack.Push(top);

        stack.Close(below, null);

        Assert.Null(await belowShown);
        Assert.False(topShown.IsCompleted);
        Assert.Same(top, stack.Top);
    }

    [Fact]
    public void Closing_twice_or_closing_something_never_shown_does_nothing()
    {
        var stack = new DialogStack<object>();
        var dialog = new object();

        stack.Push(dialog);
        Assert.True(stack.Close(dialog, null));
        Assert.False(stack.Close(dialog, null));
        Assert.False(stack.Close(new object(), null));
    }

    [Fact]
    public void The_page_is_covered_while_any_dialog_is_open()
    {
        var stack = new DialogStack<object>();
        var dialog = new object();
        Assert.False(stack.IsCovered(null));

        stack.Push(dialog);
        Assert.True(stack.IsCovered(null));

        stack.Close(dialog, null);
        Assert.False(stack.IsCovered(null));
    }

    [Fact]
    public void Only_the_top_dialog_is_uncovered()
    {
        var stack = new DialogStack<object>();
        var makeCard = new object();
        var confirm = new object();

        stack.Push(makeCard);
        Assert.False(stack.IsCovered(makeCard));

        stack.Push(confirm);
        Assert.True(stack.IsCovered(makeCard));
        Assert.False(stack.IsCovered(confirm));
    }

    [Fact]
    public void The_same_dialog_cannot_be_shown_twice_at_once()
    {
        var stack = new DialogStack<object>();
        var dialog = new object();

        stack.Push(dialog);
        Assert.Throws<InvalidOperationException>(() => { _ = stack.Push(dialog); });
    }
}
