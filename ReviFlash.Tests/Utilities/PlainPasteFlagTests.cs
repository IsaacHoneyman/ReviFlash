using Avalonia.Controls;
using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class PlainPasteFlagTests
{
    [Fact]
    public void A_paste_that_never_raises_the_event_leaves_no_mark_behind()
    {
        var box = new TextBox();
        ImageInsertion.PasteAsPlainText(box, () => { });
        Assert.False(ImageInsertion.ConsumePlainPaste(box));
    }

    [Fact]
    public void The_paste_event_during_the_call_is_let_through_once()
    {
        var box = new TextBox();
        var letThrough = new List<bool>();
        ImageInsertion.PasteAsPlainText(box, () =>
        {
            letThrough.Add(ImageInsertion.ConsumePlainPaste(box));
            letThrough.Add(ImageInsertion.ConsumePlainPaste(box));
        });
        Assert.Equal([true, false], letThrough);
    }
}
