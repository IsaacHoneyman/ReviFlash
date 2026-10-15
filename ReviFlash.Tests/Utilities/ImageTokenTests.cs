using ReviFlash.Utilities;

namespace ReviFlash.Tests.Utilities;

public class ImageTokenTests
{
    private const string Name = "0123456789abcdef0123456789abcdef.webp";

    [Fact]
    public void An_image_token_becomes_its_own_segment_between_the_text()
    {
        var segments = LatexUtility.Parse($@"Before \img{{{Name}}} after");

        Assert.Equal(3, segments.Count);
        Assert.Equal("Before ", Assert.IsType<TextSegment>(segments[0]).Text);
        Assert.Equal(new ImageSegment(Name), segments[1]);
        Assert.Equal(" after", Assert.IsType<TextSegment>(segments[2]).Text);
    }

    [Fact]
    public void A_size_sets_the_share_of_the_width()
    {
        Assert.Equal([new ImageSegment(Name, 50)], LatexUtility.Parse($@"\img[50%]{{{Name}}}"));
    }

    [Fact]
    public void Full_width_is_a_size_but_no_size_keeps_the_images_own()
    {
        Assert.Equal([new ImageSegment(Name, 100)], LatexUtility.Parse($@"\img[100%]{{{Name}}}"));
        Assert.Null(Assert.IsType<ImageSegment>(Assert.Single(LatexUtility.Parse($@"\img{{{Name}}}"))).WidthPercent);
    }

    [Theory]
    [InlineData(@"\img[0%]{a.webp}")]
    [InlineData(@"\img[150%]{a.webp}")]
    [InlineData(@"\img[50]{a.webp}")]
    [InlineData(@"\img{a.webp")]
    [InlineData(@"\\img{a.webp}")]
    public void Anything_else_stays_as_typed(string text)
    {
        Assert.DoesNotContain(LatexUtility.Parse(text), segment => segment is ImageSegment);
    }

    [Fact]
    public void Maths_never_holds_an_image()
    {
        Assert.IsType<MathSegment>(Assert.Single(LatexUtility.Parse($@"$\img{{{Name}}}$")));
    }

    [Fact]
    public void Formatting_and_reading_a_token_round_trip()
    {
        Assert.Equal(@$"\img{{{Name}}}", ImageToken.Format(Name));
        Assert.Equal(@$"\img[100%]{{{Name}}}", ImageToken.Format(Name, 100));
        Assert.Equal([new ImageSegment(Name, 30)], LatexUtility.Parse(ImageToken.Format(Name, 30)));
    }

    [Fact]
    public void Images_dont_count_as_words_in_a_note()
    {
        Assert.Equal(2, NoteText.WordCount($@"Two words \img[40%]{{{Name}}} \img{{{Name}}}"));
    }
}
