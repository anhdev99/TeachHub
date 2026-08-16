namespace TeachHub.Domain.Files;

/// <summary>
/// Checks the leading byte signature of a file instead of trusting the extension or the
/// Content-Type the client sent — exactly the NFR "validate the real file type" (SRS
/// section 9). Phase 3 reuses this same class for the files students submit.
///
/// Pure function, no I/O: callable from a handler as well as from a background job, and
/// testable in milliseconds.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = [[0x25, 0x50, 0x44, 0x46]],                       // %PDF
        [".zip"]  = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06]],
        [".pptx"] = [[0x50, 0x4B, 0x03, 0x04]],                       // OOXML is a zip
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".png"]  = [[0x89, 0x50, 0x4E, 0x47]],
        [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
    };

    /// <summary>How many leading bytes to read for the check — no need to load the whole file into memory.</summary>
    public const int HeaderBytes = 8;

    public static bool Matches(string fileName, ReadOnlySpan<byte> head)
    {
        // A double extension such as "bai-giang.pdf.exe" is stopped right here: GetExtension
        // returns ".exe", which is not in the table. But the name must still be normalized on
        // save (LocalFileStorage switches to a UUID outright and keeps only the extension) —
        // two layers of defence against two different kinds of attack.
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !Signatures.TryGetValue(ext, out var sigs)) return false;

        foreach (var sig in sigs)
            if (head.Length >= sig.Length && head[..sig.Length].SequenceEqual(sig)) return true;

        return false;
    }

    public static bool IsAllowedExtension(string fileName) =>
        Signatures.ContainsKey(Path.GetExtension(fileName));
}
