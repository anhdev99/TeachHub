using System.IO.Compression;
using Shouldly;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Phase 3 ran under the user's "no tests" instruction — this file is the agreed exception.
/// Zip-slip and zip bombs have no visible symptom in the UI: clicking around the interface cannot
/// tell a safe reader apart from a leaky one. The whole suite runs in under a second.
/// </summary>
public class SafeZipReaderTests
{
    private static MemoryStream MakeZip(params (string Path, byte[] Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in entries)
            {
                using var s = zip.CreateEntry(path).Open();
                s.Write(content);
            }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Reads_the_directory_tree_of_an_ordinary_zip()
    {
        using var zip = MakeZip(
            ("README.md", "# Bài 1"u8.ToArray()),
            ("src/App.tsx", "export default App"u8.ToArray()),
            ("src/utils/format.ts", "export const f = 1"u8.ToArray()));

        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.Files.Count.ShouldBe(3);
        tree.Files.Select(f => f.Path).ShouldContain("src/App.tsx");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\Windows\\System32\\evil.dll")]
    [InlineData("/etc/shadow")]
    [InlineData("src/../../../../root/.ssh/authorized_keys")]
    public void Rejects_a_zip_whose_paths_escape_the_extraction_root(string maliciousPath)
    {
        using var zip = MakeZip((maliciousPath, "xấu"u8.ToArray()));

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, ZipLimits.Default));
    }

    [Fact]
    public void Rejects_a_zip_exceeding_the_total_uncompressed_size()
    {
        var big = new byte[10 * 1024 * 1024];   // 10 MB of zeros -> compresses to almost nothing
        using var zip = MakeZip(("bom.bin", big));

        var limits = ZipLimits.Default with { MaxTotalUncompressedBytes = 1024 * 1024 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, limits));
    }

    [Fact]
    public void Rejects_a_zip_with_too_many_entries()
    {
        var entries = Enumerable.Range(0, 200)
            .Select(i => ($"f{i}.txt", "x"u8.ToArray())).ToArray();
        using var zip = MakeZip(entries);

        var limits = ZipLimits.Default with { MaxEntryCount = 100 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, limits));
    }

    [Fact]
    public void Rejects_a_zip_with_an_abnormal_compression_ratio()
    {
        var big = new byte[50 * 1024 * 1024];
        using var zip = MakeZip(("bom.bin", big));

        var limits = ZipLimits.Default with { MaxCompressionRatio = 50 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, limits));
    }

    [Fact]
    public void Skips_the_junk_folders_macos_and_windows_leave_behind()
    {
        using var zip = MakeZip(
            ("README.md", "x"u8.ToArray()),
            ("__MACOSX/._README.md", "x"u8.ToArray()),
            (".DS_Store", "x"u8.ToArray()),
            ("Thumbs.db", "x"u8.ToArray()));

        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.Files.Count.ShouldBe(1);
        tree.Files.Single().Path.ShouldBe("README.md");
    }

    [Fact]
    public void A_file_that_is_not_a_zip_reports_a_clear_error()
    {
        using var notAZip = new MemoryStream("đây chỉ là text"u8.ToArray());

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadTree(notAZip, ZipLimits.Default));
    }

    [Fact]
    public void The_required_structure_check_reports_exactly_what_is_missing()
    {
        using var zip = MakeZip(("src/App.tsx", "x"u8.ToArray()));   // README.md missing
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        var result = tree.CheckStructure(new RequiredStructure
        {
            RequiredFiles = ["README.md"], RequiredFolders = ["src"],
        });

        result.Passed.ShouldBeFalse();
        result.Missing.ShouldBe(["README.md"]);
    }

    [Fact]
    public void A_correct_structure_passes()
    {
        using var zip = MakeZip(("README.md", "x"u8.ToArray()), ("src/App.tsx", "x"u8.ToArray()));
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.CheckStructure(new RequiredStructure
            { RequiredFiles = ["README.md"], RequiredFolders = ["src"] }).Passed.ShouldBeTrue();
    }

    [Fact]
    public void The_structure_still_passes_when_the_student_zipped_the_containing_folder()
    {
        // The single most common mistake: the student zips the parent folder, producing
        // "BaiTap1/README.md". Hard-blocking that would flag half the class red for nothing.
        using var zip = MakeZip(("BaiTap1/README.md", "x"u8.ToArray()),
                                ("BaiTap1/src/App.tsx", "x"u8.ToArray()));
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.CheckStructure(new RequiredStructure
            { RequiredFiles = ["README.md"], RequiredFolders = ["src"] }).Passed.ShouldBeTrue();
    }

    [Fact]
    public void Reading_an_entry_by_an_escaping_path_is_rejected()
    {
        // The directory tree is already safe, but the path for the file-viewing endpoint arrives
        // from the query string.
        using var zip = MakeZip(("README.md", "x"u8.ToArray()));

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadEntry(zip, "../../etc/passwd", ZipLimits.Default, 1024 * 1024));
    }

    [Fact]
    public void Reading_an_ordinary_entry_returns_the_right_content()
    {
        using var zip = MakeZip(("src/App.tsx", "export default App"u8.ToArray()));

        var content = SafeZipReader.ReadEntry(zip, "src/App.tsx", ZipLimits.Default, 1024 * 1024);

        System.Text.Encoding.UTF8.GetString(content).ShouldBe("export default App");
    }

    [Fact]
    public void Reading_an_entry_past_the_size_limit_is_rejected()
    {
        using var zip = MakeZip(("to.txt", new byte[2 * 1024 * 1024]));

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadEntry(zip, "to.txt", ZipLimits.Default, 1024 * 1024));
    }
}
