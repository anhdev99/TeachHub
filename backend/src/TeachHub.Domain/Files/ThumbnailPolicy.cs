namespace TeachHub.Domain.Files;

/// <summary>
/// Rules for generating a preview image of a file a student submitted. Pure function, no I/O and
/// no dependency on an image-processing library: the background job in Infrastructure decides
/// whether to decode based on this, so the "decompression bomb" rule is testable in milliseconds
/// instead of requiring a malicious PNG to be built.
/// </summary>
public static class ThumbnailPolicy
{
    /// <summary>
    /// Only the extensions the signature checker in <see cref="FileSignatureValidator"/> also knows.
    /// Accepting an extra exotic format here means feeding data that never passed the signature
    /// check into the image decoder.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Ceiling on the PIXEL COUNT we allow to be decoded: 50 million px (~8000×6000, plenty for a
    /// photo from a real camera).
    ///
    /// The limit is on pixel count and NOT on file size, because file size says nothing about the
    /// size after decompression: a 10 KB PNG declaring 40000×40000 px expands to over 6 GB of
    /// memory and kills the whole worker — taking every other job in the same process with it.
    /// </summary>
    public const int MaxPixels = 50_000_000;

    /// <summary>Longest edge of the preview image. Aspect ratio preserved, no cropping.</summary>
    public const int ThumbnailMaxEdge = 320;

    public static bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// Call this with the dimensions read from the image HEADER (before the pixels are decoded).
    /// Calling it after decoding is pointless — by then the memory has already been taken.
    /// </summary>
    public static bool IsWithinPixelLimit(int width, int height) =>
        width > 0 && height > 0 && (long)width * height <= MaxPixels;
}
