namespace TeachHub.Domain.Files;

/// <summary>
/// Quy tắc sinh ảnh xem trước cho tệp sinh viên nộp. Hàm thuần, không I/O và không phụ thuộc
/// thư viện xử lý ảnh: job nền ở Infrastructure quyết định giải mã hay không dựa vào đây, nên
/// luật chặn "bom giải nén" test được trong mili-giây thay vì phải dựng một tệp PNG độc hại.
/// </summary>
public static class ThumbnailPolicy
{
    /// <summary>
    /// Chỉ những đuôi mà bộ kiểm chữ ký ở <see cref="FileSignatureValidator"/> cũng biết.
    /// Nhận thêm định dạng lạ ở đây là đưa dữ liệu chưa qua kiểm chữ ký vào bộ giải mã ảnh.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Trần SỐ ĐIỂM ẢNH cho phép giải mã: 50 triệu px (~8000×6000, thừa cho ảnh máy ảnh thật).
    ///
    /// Chặn theo số điểm ảnh chứ KHÔNG theo dung lượng tệp, vì dung lượng tệp không nói gì về
    /// kích thước sau khi giải nén: một PNG 10 KB khai 40000×40000 px nở ra hơn 6 GB bộ nhớ
    /// và giết nguyên worker — kèm theo mọi job đang chạy cùng tiến trình.
    /// </summary>
    public const int MaxPixels = 50_000_000;

    /// <summary>Cạnh dài nhất của ảnh xem trước. Giữ nguyên tỷ lệ, không cắt xén.</summary>
    public const int ThumbnailMaxEdge = 320;

    public static bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// Gọi với kích thước đọc từ HEADER ảnh (trước khi giải mã điểm ảnh). Gọi sau khi giải mã
    /// là vô nghĩa — lúc đó bộ nhớ đã bị chiếm rồi.
    /// </summary>
    public static bool IsWithinPixelLimit(int width, int height) =>
        width > 0 && height > 0 && (long)width * height <= MaxPixels;
}
