using ReviFlash.Data.Local;
using SkiaSharp;

namespace ReviFlash.Tests.Data;

[Collection(DatabaseCollection.Name)]
public class ImageStoreTests
{
    private static byte[] Png(int width, int height, SKColor colour)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(colour);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKCodec Open(string name) => SKCodec.Create(ImageStore.PathOf(name)!);

    [Fact]
    public void An_added_image_is_saved_as_webp_named_by_its_contents()
    {
        var name = ImageStore.Add(Png(64, 32, SKColors.Teal));

        Assert.True(ImageStore.IsValidName(name));
        using var codec = Open(name);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal((64, 32), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public void Adding_the_same_image_twice_stores_it_once()
    {
        var png = Png(40, 40, SKColors.Orange);

        var first = ImageStore.Add(png);
        var second = ImageStore.Add(png);

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(ImageStore.Folder, first));
        Assert.Empty(Directory.GetFiles(ImageStore.Folder, "*.partial"));
    }

    [Fact]
    public void A_big_image_is_shrunk_to_fit_keeping_its_shape()
    {
        var name = ImageStore.Add(Png(3200, 1000, SKColors.Purple));

        using var codec = Open(name);
        Assert.Equal((ImageStore.MaxSide, 500), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public void A_file_that_isnt_an_image_is_refused_with_a_reason()
    {
        var error = Assert.Throws<ImageStoreException>(() => ImageStore.Add("not an image"u8.ToArray()));

        Assert.Contains("isn't an image", error.Message);
    }

    [Fact]
    public void A_missing_file_is_refused_with_a_reason()
    {
        Assert.Throws<ImageStoreException>(() => ImageStore.AddFile(Path.Combine(TestEnvironment.DataRoot, "nowhere.png")));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.webp", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.webp", false)]
    [InlineData("0123456789abcdef0123456789abcdef.png", false)]
    [InlineData("../0123456789abcdef0123456789abcd.webp", false)]
    [InlineData("", false)]
    public void Only_names_the_store_makes_point_at_a_file(string name, bool valid)
    {
        Assert.Equal(valid, ImageStore.IsValidName(name));
        Assert.Equal(valid, ImageStore.PathOf(name) is not null);
    }
}
