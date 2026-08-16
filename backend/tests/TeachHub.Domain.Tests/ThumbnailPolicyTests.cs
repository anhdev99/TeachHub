using Shouldly;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Tests;

public class ThumbnailPolicyTests
{
    [Theory]
    [InlineData("so-do.png")]
    [InlineData("ANH.JPG")]
    [InlineData("bia.jpeg")]
    public void Recognizes_image_extensions(string fileName) =>
        ThumbnailPolicy.IsImage(fileName).ShouldBeTrue();

    [Theory]
    [InlineData("bai-nop.zip")]
    [InlineData("bao-cao.pdf")]
    [InlineData("khong-co-duoi")]
    [InlineData("")]
    public void Non_images_get_no_thumbnail(string fileName) =>
        ThumbnailPolicy.IsImage(fileName).ShouldBeFalse();

    [Fact]
    public void A_double_extension_is_judged_by_the_real_extension()
    {
        // "anh.png.exe" is an .exe, not an image. Misclassifying it here means feeding an
        // executable to the background job's image decoder — the last place anyone would look.
        ThumbnailPolicy.IsImage("anh.png.exe").ShouldBeFalse();
    }

    [Fact]
    public void Images_of_ordinary_size_are_allowed_through_the_decoder()
    {
        // A 4K screenshot still has to pass — the limit exists to stop decompression bombs,
        // not to stop real student images.
        ThumbnailPolicy.IsWithinPixelLimit(3840, 2160).ShouldBeTrue();
    }

    [Fact]
    public void A_decompression_bomb_is_blocked_BEFORE_decoding()
    {
        // A 10 KB PNG declaring 40000x40000 px expands to 6.4 GB of memory and kills the whole
        // worker. Block on the PIXEL COUNT read from the header, not on file size: file size says
        // nothing about the size after decompression.
        ThumbnailPolicy.IsWithinPixelLimit(40_000, 40_000).ShouldBeFalse();
    }

    [Fact]
    public void Negative_or_zero_dimensions_are_rejected()
    {
        // A corrupt header declaring 0 px: there is nothing to shrink, and the division inside
        // the decoder will blow up.
        ThumbnailPolicy.IsWithinPixelLimit(0, 100).ShouldBeFalse();
        ThumbnailPolicy.IsWithinPixelLimit(100, -1).ShouldBeFalse();
    }

    [Fact]
    public void Accepts_the_boundary_case_exactly_at_the_limit()
    {
        // Exactly at the ceiling is still accepted — the ceiling is "at most", not "less than".
        ThumbnailPolicy.IsWithinPixelLimit(ThumbnailPolicy.MaxPixels, 1).ShouldBeTrue();
        ThumbnailPolicy.IsWithinPixelLimit(ThumbnailPolicy.MaxPixels + 1, 1).ShouldBeFalse();
    }
}
