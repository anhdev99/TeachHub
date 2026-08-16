using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;
using TeachHub.Domain.Files;

namespace TeachHub.Infrastructure.Images;

/// <param name="Content">null khi ảnh bị từ chối — xem <paramref name="Rejection"/>.</param>
/// <param name="Rejection">Lý do từ chối bằng tiếng Việt, hoặc null khi dựng được.</param>
public sealed record ThumbnailOutcome(byte[]? Content, string? Rejection)
{
    public static ThumbnailOutcome Ok(byte[] content) => new(content, null);
    public static ThumbnailOutcome Rejected(string lyDo) => new(null, lyDo);
}

/// <summary>
/// Toàn bộ phần xử lý ảnh, tách khỏi job để test được mà không cần database lẫn storage.
/// Không chạm EF, không chạm Hangfire — chỉ nhận stream vào và trả byte ra.
///
/// Viết theo API ImageSharp <b>2.1.x</b>, bản Apache-2.0 CUỐI CÙNG (ghim ở
/// <c>Directory.Packages.props</c> vì lý do PHÁP LÝ). Nâng lên 3.x/4.x là file này không biên
/// dịch — đúng ý đồ: build đỏ ồn ào tốt hơn một lần đổi giấy phép lọt qua không ai để ý.
/// </summary>
public static class ThumbnailRenderer
{
    /// <summary>
    /// Trần bộ nhớ cho bộ cấp phát của ImageSharp. Lớp phòng thủ THỨ HAI, sau phép kiểm số
    /// điểm ảnh đọc từ header: header có thể khai một đằng còn dữ liệu điểm ảnh một nẻo, và
    /// khi đó ImageSharp ném <see cref="InvalidMemoryOperationException"/> thay vì để tiến
    /// trình bị OOM-killed — một job chết có log tốt hơn cả worker chết im lặng.
    /// </summary>
    private const int AllocationLimitMegabytes = 256;

    private static readonly Configuration Decoding = TaoCauHinh();

    private static Configuration TaoCauHinh()
    {
        var c = Configuration.Default.Clone();

        c.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = AllocationLimitMegabytes,
        });

        // Một tấm ảnh không được chiếm hết lõi CPU: job nền chạy CÙNG tiến trình với API
        // (Hangfire in-process), nên giải mã song song tối đa là làm chậm chính request của
        // sinh viên đang nộp bài kế tiếp.
        c.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);

        return c;
    }

    /// <param name="header">
    /// Stream để ĐỌC HEADER. Tách khỏi <paramref name="content"/> vì stream của S3 không tua
    /// lại được: người gọi mở hai lần thay vì đệm cả tệp 200 MB vào RAM.
    /// </param>
    /// <param name="content">Stream để giải mã điểm ảnh, mở lại từ đầu.</param>
    /// <exception cref="UnknownImageFormatException">Không nhận ra định dạng khi giải mã.</exception>
    /// <exception cref="InvalidImageContentException">Nội dung ảnh hỏng.</exception>
    public static async Task<ThumbnailOutcome> RenderAsync(
        Stream header, Stream content, CancellationToken ct)
    {
        // Đọc HEADER trước để biết kích thước mà KHÔNG giải mã điểm ảnh nào. Đây là chỗ chặn
        // "bom giải nén": một PNG 10 KB khai 40000×40000 px nở ra hơn 6 GB bộ nhớ và giết
        // nguyên worker cùng mọi job đang chạy chung tiến trình. Kiểm SAU khi giải mã là vô
        // nghĩa — lúc đó bộ nhớ đã bị chiếm rồi.
        //
        // 2.x trả về IImageInfo (interface); và trả NULL — không ném — khi không tìm được bộ
        // nhận diện phù hợp. Bỏ qua phép kiểm null là ném NullReferenceException từ trong job
        // nền cho một tệp đã đổi đuôi, đúng loại lỗi Hangfire retry mãi mà không xử lý được.
        var thongTin = await Image.IdentifyAsync(Decoding, header, ct);

        if (thongTin is null)
            return ThumbnailOutcome.Rejected("Không nhận ra định dạng ảnh.");

        if (!ThumbnailPolicy.IsWithinPixelLimit(thongTin.Width, thongTin.Height))
            return ThumbnailOutcome.Rejected(
                $"Ảnh {thongTin.Width}×{thongTin.Height} px vượt trần {ThumbnailPolicy.MaxPixels} px.");

        using var anh = await Image.LoadAsync(Decoding, content, ct);

        anh.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ThumbnailPolicy.ThumbnailMaxEdge, ThumbnailPolicy.ThumbnailMaxEdge),
            // Max giữ nguyên tỷ lệ và không cắt xén: ảnh chụp bài làm tay bị cắt mất mép là
            // mất đúng phần giảng viên cần nhìn.
            Mode = ResizeMode.Max,
        }));

        using var ms = new MemoryStream();
        await anh.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 80 }, ct);

        return ThumbnailOutcome.Ok(ms.ToArray());
    }
}
