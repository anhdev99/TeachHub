namespace TeachHub.Domain.Files;

/// <summary>
/// Kiểm tra chữ ký byte đầu file thay vì tin phần mở rộng hay Content-Type do client gửi —
/// đúng yêu cầu NFR "validate loại file thực tế" (SRS mục 9). Cùng lớp này sẽ được
/// Phase 3 dùng lại cho file sinh viên nộp.
///
/// Hàm thuần, không I/O: gọi được từ handler lẫn từ job nền, test trong mili-giây.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = [[0x25, 0x50, 0x44, 0x46]],                       // %PDF
        [".zip"]  = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06]],
        [".pptx"] = [[0x50, 0x4B, 0x03, 0x04]],                       // OOXML là zip
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".png"]  = [[0x89, 0x50, 0x4E, 0x47]],
        [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
    };

    /// <summary>Số byte đầu file cần đọc để kiểm — không cần nạp cả file vào bộ nhớ.</summary>
    public const int HeaderBytes = 8;

    public static bool Matches(string fileName, ReadOnlySpan<byte> head)
    {
        // Đuôi kép "bai-giang.pdf.exe" bị chặn ở đây: GetExtension trả ".exe", không có
        // trong bảng. Nhưng vẫn phải chuẩn hoá tên lúc lưu (LocalFileStorage đổi hẳn sang
        // UUID, chỉ giữ phần mở rộng) — hai lớp bảo vệ cho hai loại tấn công khác nhau.
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !Signatures.TryGetValue(ext, out var sigs)) return false;

        foreach (var sig in sigs)
            if (head.Length >= sig.Length && head[..sig.Length].SequenceEqual(sig)) return true;

        return false;
    }

    public static bool IsAllowedExtension(string fileName) =>
        Signatures.ContainsKey(Path.GetExtension(fileName));
}
