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
/// Reads the structure of a zip file while NEVER writing to disk under a path taken from inside
/// the zip. Every entry is checked before it is read; going over a limit stops the read right
/// there, nothing further is read.
///
/// This is the largest attack surface in the system: students upload arbitrary files and the
/// server unpacks them. Zip-slip overwrites files outside the target directory; a zip bomb fills
/// the disk. See ADR-005.
/// </summary>
public static class SafeZipReader
{
    private static readonly string[] SystemJunk = ["__MACOSX/", ".DS_Store", "Thumbs.db", "desktop.ini"];

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

            long totalUncompressed = 0, totalCompressed = 0;
            var files = new List<FileTreeNode>();

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;                       // directory
                if (SystemJunk.Any(j => entry.FullName.Contains(j, StringComparison.Ordinal))) continue;

                var entryPath = NormalizeAndGuard(entry.FullName, limits);

                totalUncompressed += entry.Length;
                totalCompressed   += entry.CompressedLength;

                if (totalUncompressed > limits.MaxTotalUncompressedBytes)
                    throw new UnsafeArchiveException(
                        $"Nội dung sau giải nén vượt {limits.MaxTotalUncompressedBytes / 1024 / 1024} MB.");

                files.Add(new FileTreeNode(entryPath, entry.Length));
            }

            // Check the compression ratio last: each individual entry can look perfectly normal
            // while the archive as a whole is still a bomb.
            if (totalCompressed > 0 && totalUncompressed / totalCompressed > limits.MaxCompressionRatio)
                throw new UnsafeArchiveException(
                    $"Tỉ lệ nén {totalUncompressed / totalCompressed}:1 bất thường, từ chối xử lý.");

            return new FileTree(files);
        }
    }

    /// <summary>
    /// Reads the content of ONE entry by path. The path has to go through exactly the same set of
    /// rules in <see cref="NormalizeAndGuard"/> as when the tree was built: the directory tree is
    /// already safe, but the path parameter arrives from the query string and the user can edit it.
    /// </summary>
    public static byte[] ReadEntry(Stream zipStream, string path, ZipLimits limits, long maxBytes)
    {
        var wanted = NormalizeAndGuard(path, limits);

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
                e => string.Equals(e.FullName.Replace('\\', '/'), wanted, StringComparison.Ordinal));

            if (entry is null)
                throw new UnsafeArchiveException($"Không có mục '{path}' trong file nén.");

            if (entry.Length > maxBytes)
                throw new UnsafeArchiveException(
                    $"Tệp '{path}' nặng {entry.Length / 1024} KB, vượt giới hạn xem trực tiếp.");

            using var s = entry.Open();
            using var buf = new MemoryStream();

            // Bounded copy: entry.Length is a number the archive itself declares, and an attacker
            // can forge it.
            var remaining = maxBytes + 1;
            var chunk = new byte[81920];
            int read;
            while (remaining > 0 && (read = s.Read(chunk, 0, (int)Math.Min(chunk.Length, remaining))) > 0)
            {
                buf.Write(chunk, 0, read);
                remaining -= read;
            }

            if (buf.Length > maxBytes)
                throw new UnsafeArchiveException($"Tệp '{path}' vượt giới hạn xem trực tiếp.");

            return buf.ToArray();
        }
    }

    /// <summary>
    /// Normalizes a path and blocks malicious ones. Exposed as <c>public</c> on purpose: the
    /// Application tier has to call this exact function for the path parameter that comes from the
    /// query string. Copying the logic into another tier creates two copies of the same set of
    /// rules, and they will drift apart on the very first edit.
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

    /// <summary>EF needs a parameterless constructor to materialize the jsonb column.</summary>
    public FileTree() : this([]) { }

    public StructureCheckResult CheckStructure(RequiredStructure required)
    {
        // Try BOTH readings and take whichever matches better, instead of hard-coding one of them.
        //
        // Reading 1 — take it literally: the files sit at the root of the archive.
        // Reading 2 — strip one wrapper folder: the student right-clicks a folder and picks
        //             "Compress", which produces "BaiTap1/README.md". That is the default
        //             behaviour of both Windows and macOS; rejecting it would raise a false red
        //             flag on half the class.
        //
        // Stripping only, without trying reading 1, breaks the exact opposite case: a submission
        // containing just src/App.tsx would have the very "src" folder that is being required
        // stripped away, and would report "src/" missing while it is sitting right there.
        var asIs = MissingFrom(Files.Select(f => f.Path).ToList(), required);

        var root = CommonRootFolder();
        if (root is null) return new StructureCheckResult(asIs.Count == 0, asIs);

        var stripped = MissingFrom([.. Files.Select(f => f.Path[(root.Length + 1)..])], required);

        var missing = stripped.Count < asIs.Count ? stripped : asIs;
        return new StructureCheckResult(missing.Count == 0, missing);
    }

    private static List<string> MissingFrom(List<string> paths, RequiredStructure required)
    {
        var missing = new List<string>();

        foreach (var f in required.RequiredFiles)
            if (!paths.Any(p => string.Equals(p, f, StringComparison.OrdinalIgnoreCase)))
                missing.Add(f);

        foreach (var d in required.RequiredFolders)
            if (!paths.Any(p => p.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)))
                missing.Add(d + "/");

        return missing;
    }

    private string? CommonRootFolder()
    {
        if (Files.Count == 0) return null;

        var first = Files[0].Path.Split('/');
        if (first.Length < 2) return null;

        var candidate = first[0];
        return Files.All(f => f.Path.StartsWith(candidate + "/", StringComparison.Ordinal))
            ? candidate : null;
    }
}
