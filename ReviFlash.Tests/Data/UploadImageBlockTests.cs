using ReviFlash.Data.Local;
using ReviFlash.Models;

namespace ReviFlash.Tests.Data;

public class UploadImageBlockTests
{
    private const string Image = @"\img[50%]{0123456789abcdef0123456789abcdef.webp}";

    public static TheoryData<FlashCard> CardsWithAnImage() =>
    [
        new FlipFlashCard("Q " + Image, "A"),
        new FlipFlashCard("Q", Image),
        new ClozeFlashCard(@"The mascot is \C{" + Image + "}", "", typeAnswer: false),
        new MultiFlashCard("Pick", "", [("plain", true), (Image, false)]),
        new MatchFlashCard("Match The Cards", "Match The Cards", [("left", "right"), ("other", Image)]),
        new TrueFalseFlashCard("Q", "", true, Image, "False"),
    ];

    [Theory]
    [MemberData(nameof(CardsWithAnImage))]
    public void An_image_anywhere_on_a_card_blocks_the_upload(FlashCard card)
    {
        Assert.True(DeckTransferManager.HasImages([new FlipFlashCard("Plain", "Card"), card]));
    }

    [Fact]
    public void Cards_without_images_can_be_uploaded()
    {
        Assert.False(DeckTransferManager.HasImages([new FlipFlashCard(@"Text with $\pi$ and \B{bold}", "A"), new FlipFlashCard(@"\\img{escaped}", "B")]));
    }
}
