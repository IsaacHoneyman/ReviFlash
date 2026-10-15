using ReviFlash.Utilities;
using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class ImageInsertionTests
{
    private const string Token = @"\img{x.webp}";

    private static (string Text, int Caret) Insert(string text, int start, int end)
    {
        var edit = ImageInsertion.InsertOnOwnLine(text, start, end, Token);
        Assert.Equal(edit.SelectionStart, edit.SelectionEnd);
        return (edit.Text, edit.SelectionStart);
    }

    [Fact]
    public void An_image_in_the_middle_of_a_line_gets_a_line_of_its_own()
    {
        Assert.Equal(("Before\n" + Token + "\nafter", 7 + Token.Length), Insert("Before after", 6, 7));
    }

    [Fact]
    public void No_extra_line_breaks_where_the_line_already_ends()
    {
        Assert.Equal(("One\n" + Token + "\nTwo", 4 + Token.Length), Insert("One\n\nTwo", 4, 4));
        Assert.Equal((Token, Token.Length), Insert("", 0, 0));
        Assert.Equal(("Text\n" + Token, 5 + Token.Length), Insert("Text", 4, 4));
    }

    [Fact]
    public void A_selection_is_replaced_by_the_image()
    {
        Assert.Equal(("Keep\n" + Token + "\nend", 5 + Token.Length), Insert("Keep this end", 4, 10));
    }

    [Fact]
    public void A_blank_hiding_an_image_is_found_but_one_beside_it_is_not()
    {
        Assert.True(ClozeUtility.AnyBlankHasImage(@"The mascot is \C{\img{a.webp}}."));
        Assert.True(ClozeUtility.AnyBlankHasImage(@"\C1{text} and \C2{\img[40%]{a.webp}}"));
        Assert.False(ClozeUtility.AnyBlankHasImage(@"\img{a.webp} is \C{the mascot}."));
    }
}
