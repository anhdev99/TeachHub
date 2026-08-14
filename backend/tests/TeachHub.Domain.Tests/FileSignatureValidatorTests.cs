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
    public void Pdf_that_duoc_chap_nhan() => FileSignatureValidator.Matches("slide.pdf", Pdf).ShouldBeTrue();

    [Fact]
    public void Pptx_la_zip_nen_chu_ky_PK_duoc_chap_nhan() =>
        FileSignatureValidator.Matches("bai-giang.pptx", Zip).ShouldBeTrue();

    [Fact]
    public void File_exe_doi_ten_thanh_pdf_bi_tu_choi()
    {
        // Đây là ca tấn công thật: đổi tên rồi upload. Tin phần mở rộng là đủ để nhận mã độc.
        FileSignatureValidator.Matches("bai-giang.pdf", Exe).ShouldBeFalse();
    }

    [Fact]
    public void Duoi_kep_bi_chan_vi_phan_mo_rong_that_la_exe()
    {
        // Path.GetExtension("bai-giang.pdf.exe") trả ".exe" — không có trong bảng chữ ký.
        FileSignatureValidator.Matches("bai-giang.pdf.exe", Pdf).ShouldBeFalse();
    }

    [Fact]
    public void Phan_mo_rong_khong_nam_trong_danh_sach_cho_phep_bi_tu_choi() =>
        FileSignatureValidator.Matches("script.sh", Pdf).ShouldBeFalse();

    [Fact]
    public void Khong_phan_biet_hoa_thuong_o_phan_mo_rong() =>
        FileSignatureValidator.Matches("SLIDE.PDF", Pdf).ShouldBeTrue();

    [Fact]
    public void File_rong_hoac_qua_ngan_bi_tu_choi() =>
        FileSignatureValidator.Matches("slide.pdf", []).ShouldBeFalse();

    [Fact]
    public void Png_that_duoc_chap_nhan() => FileSignatureValidator.Matches("anh.png", Png).ShouldBeTrue();

    [Fact]
    public void Ten_file_khong_co_phan_mo_rong_bi_tu_choi() =>
        FileSignatureValidator.Matches("khong-co-duoi", Pdf).ShouldBeFalse();
}
