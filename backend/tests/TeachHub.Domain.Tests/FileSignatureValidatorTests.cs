using Shouldly;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Tests;

public class FileSignatureValidatorTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D];        // "%PDF-"
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];              // PK..
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00];              // "MZ"

    [Fact]
    public void A_real_pdf_is_accepted() => FileSignatureValidator.Matches("slide.pdf", Pdf).ShouldBeTrue();

    [Fact]
    public void Pptx_is_a_zip_so_the_PK_signature_is_accepted() =>
        FileSignatureValidator.Matches("bai-giang.pptx", Zip).ShouldBeTrue();

    [Fact]
    public void An_exe_renamed_to_pdf_is_rejected()
    {
        // This is a real attack: rename, then upload. Trusting the extension is enough to accept malware.
        FileSignatureValidator.Matches("bai-giang.pdf", Exe).ShouldBeFalse();
    }

    [Fact]
    public void A_double_extension_is_blocked_because_the_real_extension_is_exe()
    {
        // Path.GetExtension("bai-giang.pdf.exe") returns ".exe" — not present in the signature table.
        FileSignatureValidator.Matches("bai-giang.pdf.exe", Pdf).ShouldBeFalse();
    }

    [Fact]
    public void An_extension_outside_the_allowlist_is_rejected() =>
        FileSignatureValidator.Matches("script.sh", Pdf).ShouldBeFalse();

    [Fact]
    public void The_extension_is_matched_case_insensitively() =>
        FileSignatureValidator.Matches("SLIDE.PDF", Pdf).ShouldBeTrue();

    [Fact]
    public void An_empty_or_too_short_file_is_rejected() =>
        FileSignatureValidator.Matches("slide.pdf", []).ShouldBeFalse();

    [Fact]
    public void A_real_png_is_accepted() => FileSignatureValidator.Matches("anh.png", Png).ShouldBeTrue();

    [Fact]
    public void A_file_name_with_no_extension_is_rejected() =>
        FileSignatureValidator.Matches("khong-co-duoi", Pdf).ShouldBeFalse();
}
