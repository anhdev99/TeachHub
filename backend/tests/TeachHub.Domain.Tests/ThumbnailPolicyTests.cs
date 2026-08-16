using Shouldly;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Tests;

public class ThumbnailPolicyTests
{
    [Theory]
    [InlineData("so-do.png")]
    [InlineData("ANH.JPG")]
    [InlineData("bia.jpeg")]
    public void Nhan_dung_phan_mo_rong_anh(string ten) =>
        ThumbnailPolicy.IsImage(ten).ShouldBeTrue();

    [Theory]
    [InlineData("bai-nop.zip")]
    [InlineData("bao-cao.pdf")]
    [InlineData("khong-co-duoi")]
    [InlineData("")]
    public void Khong_phai_anh_thi_khong_tao_thumbnail(string ten) =>
        ThumbnailPolicy.IsImage(ten).ShouldBeFalse();

    [Fact]
    public void Duoi_kep_tinh_theo_phan_mo_rong_that()
    {
        // "anh.png.exe" là .exe chứ không phải ảnh. Nhận nhầm ở đây nghĩa là đưa một tệp
        // thực thi vào bộ giải mã ảnh của job nền — chỗ không ai ngờ tới nhất.
        ThumbnailPolicy.IsImage("anh.png.exe").ShouldBeFalse();
    }

    [Fact]
    public void Anh_kich_thuoc_thuong_gap_thi_cho_giai_ma()
    {
        // Ảnh chụp màn hình 4K vẫn phải qua được — giới hạn là để chặn bom nén, không phải
        // để chặn ảnh thật của sinh viên.
        ThumbnailPolicy.IsWithinPixelLimit(3840, 2160).ShouldBeTrue();
    }

    [Fact]
    public void Bom_giai_nen_bi_chan_TRUOC_khi_giai_ma()
    {
        // Một PNG 10 KB khai 40000×40000 px nở ra 6.4 GB bộ nhớ và giết cả worker. Chặn theo
        // SỐ ĐIỂM ẢNH đọc từ header, không theo dung lượng tệp: dung lượng tệp không nói gì
        // về kích thước sau khi giải nén.
        ThumbnailPolicy.IsWithinPixelLimit(40_000, 40_000).ShouldBeFalse();
    }

    [Fact]
    public void Kich_thuoc_am_hoac_bang_khong_bi_tu_choi()
    {
        // Header hỏng khai 0 px: không có gì để thu nhỏ, và phép chia trong bộ giải mã sẽ nổ.
        ThumbnailPolicy.IsWithinPixelLimit(0, 100).ShouldBeFalse();
        ThumbnailPolicy.IsWithinPixelLimit(100, -1).ShouldBeFalse();
    }

    [Fact]
    public void Nhan_dieu_kien_bien_dung_bang_gioi_han()
    {
        // Đúng bằng trần vẫn được nhận — trần là "tối đa", không phải "nhỏ hơn".
        ThumbnailPolicy.IsWithinPixelLimit(ThumbnailPolicy.MaxPixels, 1).ShouldBeTrue();
        ThumbnailPolicy.IsWithinPixelLimit(ThumbnailPolicy.MaxPixels + 1, 1).ShouldBeFalse();
    }
}
