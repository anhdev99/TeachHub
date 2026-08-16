using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TeachHub.Infrastructure.Images;

namespace TeachHub.Api.Tests;

/// <summary>
/// Test thuần cho phần xử lý ảnh — KHÔNG dùng container. Bộ dựng thumbnail không chạm database
/// lẫn storage, và thứ dễ sai nhất ở đây (bom giải nén lọt qua, hoặc file rác làm nổ job nền)
/// chỉ kiểm được bằng cách đưa đúng những byte đó vào.
/// </summary>
public class ThumbnailRendererTests
{
    /// <summary>
    /// PNG hợp lệ 64×48, sinh bằng chính ImageSharp thay vì kèm một tệp nhị phân vào repo:
    /// tệp nhị phân trong repo không ai đọc được lúc review, và một base64 gõ tay sai CRC làm
    /// test đỏ vì lý do chẳng liên quan gì tới thứ đang kiểm.
    /// </summary>
    private static byte[] PngHopLe()
    {
        using var anh = new Image<Rgba32>(64, 48);
        using var ms = new MemoryStream();
        anh.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// PNG khai 40000×40000 px trong IHDR nhưng chỉ nặng vài trăm byte — đúng khuôn "bom giải
    /// nén". Chỉ cần header hợp lệ vì phép chặn chạy TRƯỚC khi giải mã điểm ảnh.
    /// </summary>
    private static byte[] PngBomGiaiNen()
    {
        var png = PngHopLe();

        // Bố cục PNG: 8 byte chữ ký, 4 byte độ dài chunk, 4 byte "IHDR", rồi 13 byte dữ liệu
        // (width ở offset 16, height ở offset 20, big-endian), rồi 4 byte CRC ở offset 29.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 40_000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 40_000);

        // Tính lại CRC trên "IHDR" + 13 byte dữ liệu. Không tính lại thì bộ giải mã ném
        // "CRC Error" và test xanh vì SAI lý do — nó chứng minh CRC hỏng bị chặn, chứ không
        // chứng minh bom giải nén bị chặn.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            png.AsSpan(29), Crc32(png.AsSpan(12, 17)));

        return png;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static Task<ThumbnailOutcome> Dung(byte[] bytes) =>
        ThumbnailRenderer.RenderAsync(
            new MemoryStream(bytes), new MemoryStream(bytes), CancellationToken.None);

    [Fact]
    public async Task Anh_hop_le_cho_ra_JPEG()
    {
        var kq = await Dung(PngHopLe());

        kq.Rejection.ShouldBeNull();
        kq.Content.ShouldNotBeNull();

        // Chữ ký JPEG: FF D8 FF. Khẳng định đúng ĐỊNH DẠNG ĐẦU RA chứ không chỉ "có byte":
        // trả về nguyên tệp gốc cũng làm test "không rỗng" xanh.
        kq.Content!.Length.ShouldBeGreaterThan(3);
        kq.Content[0].ShouldBe((byte)0xFF);
        kq.Content[1].ShouldBe((byte)0xD8);
        kq.Content[2].ShouldBe((byte)0xFF);
    }

    [Fact]
    public async Task Bom_giai_nen_bi_TU_CHOI_chu_khong_lam_no_job()
    {
        // Một PNG vài chục byte khai 40000×40000 px nở ra hơn 6 GB. Nếu phép chặn chạy sau khi
        // giải mã thì test này không trả về — nó giết luôn tiến trình chạy test.
        var kq = await Dung(PngBomGiaiNen());

        kq.Content.ShouldBeNull();
        kq.Rejection.ShouldNotBeNull();
        kq.Rejection!.ShouldContain("40000");
    }

    [Fact]
    public async Task File_rac_bi_tu_choi_hoac_nem_loi_MONG_DOI()
    {
        // Tệp đổi đuôi thành .png. ImageSharp 2.1 trả null từ Identify (không ném), nhưng
        // hợp đồng vẫn phải chấp nhận cả nhánh ném để job bắt đúng loại exception.
        var rac = "day khong phai anh"u8.ToArray();

        try
        {
            (await Dung(rac)).Content.ShouldBeNull();
        }
        catch (Exception e) when (e is UnknownImageFormatException
                                    or InvalidImageContentException)
        {
            // Nhánh này cũng hợp lệ — job đã bắt đúng hai kiểu exception đó.
        }
    }
}
