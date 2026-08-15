using System.IO.Compression;
using TeachHub.Domain.Assignments;

namespace TeachHub.Domain.Files;

public sealed class UnsafeArchiveException(string message) : Exception(message);

public sealed record ZipLimits
{
    public long MaxTotalUncompressedBytes { get; init; } = 200L * 1024 * 1024;   // 200 MB
    public int MaxEntryCount { get; init; } = 5_000;
    public int MaxCompressionRatio { get; init; } = 100;
    public int MaxPathDepth { get; init; } = 20;

    public static ZipLimits Default => new();
}

public sealed record FileTreeNode(string Path, long SizeBytes);

public sealed record StructureCheckResult(bool Passed, List<string> Missing);

/// <summary>
/// Đọc cấu trúc file zip mà KHÔNG BAO GIỜ ghi ra đĩa theo đường dẫn nằm trong file zip.
/// Mọi entry bị kiểm trước khi đọc; vượt hạn mức là dừng ngay, không đọc tiếp.
///
/// Đây là bề mặt tấn công lớn nhất của hệ thống: sinh viên upload file tùy ý và máy chủ
/// giải nén chúng. Zip-slip ghi đè file ngoài thư mục đích; zip bomb làm đầy đĩa. Xem ADR-005.
/// </summary>
public static class SafeZipReader
{
    private static readonly string[] RacHeThong = ["__MACOSX/", ".DS_Store", "Thumbs.db", "desktop.ini"];

    public static FileTree ReadTree(Stream zipStream, ZipLimits limits)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException e)
        {
            throw new UnsafeArchiveException($"File không phải định dạng ZIP hợp lệ: {e.Message}");
        }

        using (archive)
        {
            if (archive.Entries.Count > limits.MaxEntryCount)
                throw new UnsafeArchiveException(
                    $"File nén có {archive.Entries.Count} mục, vượt giới hạn {limits.MaxEntryCount}. " +
                    "Có thể bạn đã nén nhầm cả thư mục node_modules hoặc bin/obj.");

            long tongGiaiNen = 0, tongNen = 0;
            var files = new List<FileTreeNode>();

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;                       // thư mục
                if (RacHeThong.Any(r => entry.FullName.Contains(r, StringComparison.Ordinal))) continue;

                var duongDan = NormalizeAndGuard(entry.FullName, limits);

                tongGiaiNen += entry.Length;
                tongNen     += entry.CompressedLength;

                if (tongGiaiNen > limits.MaxTotalUncompressedBytes)
                    throw new UnsafeArchiveException(
                        $"Nội dung sau giải nén vượt {limits.MaxTotalUncompressedBytes / 1024 / 1024} MB.");

                files.Add(new FileTreeNode(duongDan, entry.Length));
            }

            // Kiểm tỉ lệ nén sau cùng: từng entry lẻ có thể bình thường nhưng tổng thể
            // vẫn là một quả bom.
            if (tongNen > 0 && tongGiaiNen / tongNen > limits.MaxCompressionRatio)
                throw new UnsafeArchiveException(
                    $"Tỉ lệ nén {tongGiaiNen / tongNen}:1 bất thường, từ chối xử lý.");

            return new FileTree(files);
        }
    }

    /// <summary>
    /// Đọc nội dung MỘT entry theo đường dẫn. Đường dẫn phải đi qua đúng bộ luật của
    /// <see cref="NormalizeAndGuard"/> như lúc dựng cây: cây thư mục đã an toàn nhưng
    /// tham số path đến từ query string và người dùng sửa được.
    /// </summary>
    public static byte[] ReadEntry(Stream zipStream, string path, ZipLimits limits, long maxBytes)
    {
        var canDoc = NormalizeAndGuard(path, limits);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException e)
        {
            throw new UnsafeArchiveException($"File không phải định dạng ZIP hợp lệ: {e.Message}");
        }

        using (archive)
        {
            var entry = archive.Entries.FirstOrDefault(
                e => string.Equals(e.FullName.Replace('\\', '/'), canDoc, StringComparison.Ordinal));

            if (entry is null)
                throw new UnsafeArchiveException($"Không có mục '{path}' trong file nén.");

            if (entry.Length > maxBytes)
                throw new UnsafeArchiveException(
                    $"Tệp '{path}' nặng {entry.Length / 1024} KB, vượt giới hạn xem trực tiếp.");

            using var s = entry.Open();
            using var buf = new MemoryStream();

            // Chép có giới hạn: entry.Length là số do chính file nén khai, kẻ tấn công sửa được.
            var conLai = maxBytes + 1;
            var tam = new byte[81920];
            int doc;
            while (conLai > 0 && (doc = s.Read(tam, 0, (int)Math.Min(tam.Length, conLai))) > 0)
            {
                buf.Write(tam, 0, doc);
                conLai -= doc;
            }

            if (buf.Length > maxBytes)
                throw new UnsafeArchiveException($"Tệp '{path}' vượt giới hạn xem trực tiếp.");

            return buf.ToArray();
        }
    }

    /// <summary>
    /// Chuẩn hóa và chặn đường dẫn độc hại. Phơi ra <c>public</c> có chủ ý: tầng Application
    /// phải gọi lại đúng hàm này cho tham số path đến từ query string. Chép logic sang tầng
    /// khác là tạo hai bản sao của cùng một bộ luật, và chúng sẽ lệch nhau ngay lần sửa đầu.
    /// </summary>
    public static string NormalizeAndGuard(string raw, ZipLimits limits)
    {
        var path = raw.Replace('\\', '/');

        if (Path.IsPathRooted(path) || path.StartsWith('/'))
            throw new UnsafeArchiveException($"Đường dẫn tuyệt đối trong file nén: '{raw}'.");

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Contains(".."))
            throw new UnsafeArchiveException($"Đường dẫn thoát khỏi thư mục đích (zip-slip): '{raw}'.");

        if (parts.Length > limits.MaxPathDepth)
            throw new UnsafeArchiveException($"Đường dẫn quá sâu ({parts.Length} cấp): '{raw}'.");

        return string.Join('/', parts);
    }
}

public sealed class FileTree(List<FileTreeNode> files)
{
    public List<FileTreeNode> Files { get; set; } = files;

    /// <summary>EF cần hàm dựng không tham số để nạp cột jsonb.</summary>
    public FileTree() : this([]) { }

    public StructureCheckResult CheckStructure(RequiredStructure required)
    {
        // Thử HAI cách hiểu rồi lấy cách khớp hơn, thay vì chọn cứng một cách.
        //
        // Cách 1 — đọc nguyên: file nằm ngay ở gốc file nén.
        // Cách 2 — bóc một lớp thư mục bọc: sinh viên chuột phải vào thư mục rồi "nén",
        //          ra "BaiTap1/README.md". Đây là hành vi mặc định của Windows và macOS,
        //          chặn nó sẽ gắn cờ đỏ oan cho nửa lớp.
        //
        // Chỉ bóc mà không thử cách 1 thì hỏng đúng ca ngược lại: một bài chỉ có src/App.tsx
        // sẽ bị bóc mất chính thư mục "src" đang được yêu cầu, và báo thiếu "src/" trong khi
        // nó nằm ngay đó.
        var nguyen = ThieuGi(Files.Select(f => f.Path).ToList(), required);

        var goc = CommonRootFolder();
        if (goc is null) return new StructureCheckResult(nguyen.Count == 0, nguyen);

        var daBoc = ThieuGi([.. Files.Select(f => f.Path[(goc.Length + 1)..])], required);

        var thieu = daBoc.Count < nguyen.Count ? daBoc : nguyen;
        return new StructureCheckResult(thieu.Count == 0, thieu);
    }

    private static List<string> ThieuGi(List<string> duongDan, RequiredStructure required)
    {
        var thieu = new List<string>();

        foreach (var f in required.RequiredFiles)
            if (!duongDan.Any(p => string.Equals(p, f, StringComparison.OrdinalIgnoreCase)))
                thieu.Add(f);

        foreach (var d in required.RequiredFolders)
            if (!duongDan.Any(p => p.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)))
                thieu.Add(d + "/");

        return thieu;
    }

    private string? CommonRootFolder()
    {
        if (Files.Count == 0) return null;

        var dau = Files[0].Path.Split('/');
        if (dau.Length < 2) return null;

        var ungVien = dau[0];
        return Files.All(f => f.Path.StartsWith(ungVien + "/", StringComparison.Ordinal))
            ? ungVien : null;
    }
}
