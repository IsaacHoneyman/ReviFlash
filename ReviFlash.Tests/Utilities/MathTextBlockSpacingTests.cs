using Avalonia.Controls;
using Avalonia.Controls.Documents;
using ReviFlash.Utilities;
using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class MathTextBlockSpacingTests
{
    private static StackPanel Build(string text) => new MathText().BuildView(LatexUtility.Parse(text));

    private static List<string> Shape(StackPanel panel) => [.. panel.Children.Select(child => child switch
    {
        TextBlock block => "P:" + string.Concat(block.Inlines!.Select(inline => inline is LineBreak ? "|" : ((Run)inline).Text)),
        Grid { Tag: "bullet" } row => "B:" + string.Concat(row.Children.OfType<TextBlock>().Last().Inlines!.Select(inline => ((Run)inline).Text)),
        _ => "X",
    })];

    [Fact]
    public void A_line_break_either_side_of_an_image_adds_no_blank_line()
    {
        Assert.Equal(["P:a", "X", "P:b"], Shape(Build("a\n\\img{missing.png}\nb")));
    }

    [Fact]
    public void An_extra_empty_line_beside_an_image_still_shows()
    {
        Assert.Equal(["P:a|", "X", "P:|b"], Shape(Build("a\n\n\\img{missing.png}\n\nb")));
    }

    [Fact]
    public void An_image_straight_after_text_with_no_line_break_is_unchanged()
    {
        Assert.Equal(["P:a", "X"], Shape(Build("a\\img{missing.png}")));
    }

    [Fact]
    public void A_bulleted_display_formula_that_fails_to_typeset_keeps_its_bullet()
    {
        var shape = Shape(Build("- $$\\notacommand{$$"));
        Assert.StartsWith("B:", Assert.Single(shape));
    }
}
