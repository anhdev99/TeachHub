using System.IO.Compression;
using Shouldly;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Phase 3 chạy theo yêu cầu "không viết test" của người dùng — file này là ngoại lệ đã
/// thống nhất. Zip-slip và zip bomb không có biểu hiện nào trên giao diện: bấm thử trên UI
/// không phân biệt được bộ đọc an toàn với bộ đọc thủng. Toàn bộ suite này chạy dưới 1 giây.
/// </summary>
public class SafeZipReaderTests
{
    private static MemoryStream TaoZip(params (string Path, byte[] Content)[] entries)
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
    public void Doc_duoc_cay_thu_muc_cua_zip_binh_thuong()
    {
        using var zip = TaoZip(
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
    public void Tu_choi_zip_co_duong_dan_thoat_ra_ngoai(string duongDanAc)
    {
        using var zip = TaoZip((duongDanAc, "xấu"u8.ToArray()));

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, ZipLimits.Default));
    }

    [Fact]
    public void Tu_choi_zip_vuot_qua_tong_dung_luong_sau_giai_nen()
    {
        var to = new byte[10 * 1024 * 1024];   // 10 MB toàn số 0 → nén cực nhỏ
        using var zip = TaoZip(("bom.bin", to));

        var hanMuc = ZipLimits.Default with { MaxTotalUncompressedBytes = 1024 * 1024 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, hanMuc));
    }

    [Fact]
    public void Tu_choi_zip_co_qua_nhieu_entry()
    {
        var entries = Enumerable.Range(0, 200)
            .Select(i => ($"f{i}.txt", "x"u8.ToArray())).ToArray();
        using var zip = TaoZip(entries);

        var hanMuc = ZipLimits.Default with { MaxEntryCount = 100 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, hanMuc));
    }

    [Fact]
    public void Tu_choi_zip_co_ti_le_nen_bat_thuong()
    {
        var to = new byte[50 * 1024 * 1024];
        using var zip = TaoZip(("bom.bin", to));

        var hanMuc = ZipLimits.Default with { MaxCompressionRatio = 50 };

        Should.Throw<UnsafeArchiveException>(() => SafeZipReader.ReadTree(zip, hanMuc));
    }

    [Fact]
    public void Bo_qua_thu_muc_rac_cua_macos_va_windows()
    {
        using var zip = TaoZip(
            ("README.md", "x"u8.ToArray()),
            ("__MACOSX/._README.md", "x"u8.ToArray()),
            (".DS_Store", "x"u8.ToArray()),
            ("Thumbs.db", "x"u8.ToArray()));

        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.Files.Count.ShouldBe(1);
        tree.Files.Single().Path.ShouldBe("README.md");
    }

    [Fact]
    public void File_khong_phai_zip_bao_loi_ro_rang()
    {
        using var khongPhaiZip = new MemoryStream("đây chỉ là text"u8.ToArray());

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadTree(khongPhaiZip, ZipLimits.Default));
    }

    [Fact]
    public void Kiem_tra_cau_truc_bat_buoc_bao_thieu_dung_muc()
    {
        using var zip = TaoZip(("src/App.tsx", "x"u8.ToArray()));   // thiếu README.md
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        var kq = tree.CheckStructure(new RequiredStructure
        {
            RequiredFiles = ["README.md"], RequiredFolders = ["src"],
        });

        kq.Passed.ShouldBeFalse();
        kq.Missing.ShouldBe(["README.md"]);
    }

    [Fact]
    public void Cau_truc_dung_thi_pass()
    {
        using var zip = TaoZip(("README.md", "x"u8.ToArray()), ("src/App.tsx", "x"u8.ToArray()));
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.CheckStructure(new RequiredStructure
            { RequiredFiles = ["README.md"], RequiredFolders = ["src"] }).Passed.ShouldBeTrue();
    }

    [Fact]
    public void Cau_truc_van_dung_khi_sv_nen_ca_thu_muc_goc()
    {
        // Lỗi phổ biến nhất: sinh viên nén cả thư mục cha thành "BaiTap1/README.md".
        // Chặn cứng chuyện này sẽ khiến nửa lớp bị gắn cờ đỏ oan.
        using var zip = TaoZip(("BaiTap1/README.md", "x"u8.ToArray()),
                               ("BaiTap1/src/App.tsx", "x"u8.ToArray()));
        var tree = SafeZipReader.ReadTree(zip, ZipLimits.Default);

        tree.CheckStructure(new RequiredStructure
            { RequiredFiles = ["README.md"], RequiredFolders = ["src"] }).Passed.ShouldBeTrue();
    }

    [Fact]
    public void Doc_entry_theo_duong_dan_thoat_ra_ngoai_bi_tu_choi()
    {
        // Cây thư mục đã an toàn, nhưng path của endpoint xem file đến từ query string.
        using var zip = TaoZip(("README.md", "x"u8.ToArray()));

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadEntry(zip, "../../etc/passwd", ZipLimits.Default, 1024 * 1024));
    }

    [Fact]
    public void Doc_entry_binh_thuong_tra_dung_noi_dung()
    {
        using var zip = TaoZip(("src/App.tsx", "export default App"u8.ToArray()));

        var noiDung = SafeZipReader.ReadEntry(zip, "src/App.tsx", ZipLimits.Default, 1024 * 1024);

        System.Text.Encoding.UTF8.GetString(noiDung).ShouldBe("export default App");
    }

    [Fact]
    public void Doc_entry_vuot_gioi_han_dung_luong_bi_tu_choi()
    {
        using var zip = TaoZip(("to.txt", new byte[2 * 1024 * 1024]));

        Should.Throw<UnsafeArchiveException>(
            () => SafeZipReader.ReadEntry(zip, "to.txt", ZipLimits.Default, 1024 * 1024));
    }
}
