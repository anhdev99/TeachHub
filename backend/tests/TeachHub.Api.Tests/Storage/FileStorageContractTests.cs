using Microsoft.Extensions.Options;
using Shouldly;
using TeachHub.Application.Common;
using TeachHub.Infrastructure.Storage;

namespace TeachHub.Api.Tests.Storage;

/// <summary>
/// Cả hai adapter phải qua CÙNG một bộ test. Nếu không, đổi Local → S3 lúc lên production
/// sẽ vỡ theo những cách không ai lường trước — và lúc đó thứ mất là bài nộp của sinh viên.
/// </summary>
public abstract class FileStorageContractTests
{
    protected abstract IFileStorage Storage { get; }

    [Fact]
    public async Task Luu_roi_doc_lai_ra_dung_noi_dung()
    {
        var noiDung = "xin chào cả nhà"u8.ToArray();
        var saved = await Storage.SaveAsync("test", "a.txt",
            new MemoryStream(noiDung), "text/plain", CancellationToken.None);

        await using var stream = await Storage.OpenReadAsync(saved.Path, CancellationToken.None);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().ShouldBe(noiDung);
    }

    [Fact]
    public async Task Ten_file_trung_khong_ghi_de_len_nhau()
    {
        var a = await Storage.SaveAsync("test", "same.txt", new MemoryStream([1]), "text/plain", default);
        var b = await Storage.SaveAsync("test", "same.txt", new MemoryStream([2]), "text/plain", default);
        b.Path.ShouldNotBe(a.Path);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\x")]
    [InlineData("/etc/shadow")]
    public async Task Ten_file_doc_hai_khong_thoat_duoc_ra_ngoai_thu_muc(string tenAc)
    {
        var saved = await Storage.SaveAsync("test", tenAc, new MemoryStream([1]), "text/plain", default);
        saved.Path.ShouldNotContain("..");
        saved.Path.ShouldStartWith("test/");
    }

    [Fact]
    public async Task Xoa_roi_thi_doc_lai_bao_loi()
    {
        var saved = await Storage.SaveAsync("test", "b.txt", new MemoryStream([1]), "text/plain", default);
        await Storage.DeleteAsync(saved.Path, default);
        await Should.ThrowAsync<FileNotFoundException>(() => Storage.OpenReadAsync(saved.Path, default));
    }

    [Fact]
    public async Task Doc_duong_dan_thoat_khoi_thu_muc_bi_tu_choi()
    {
        // Đường dẫn độc hại không chỉ đến từ tên file lúc upload mà còn từ tham số
        // đường dẫn khi tải về. Chốt chặn phải nằm ở cả hai đầu.
        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => Storage.OpenReadAsync("../../etc/passwd", default));
    }
}

public sealed class LocalFileStorageTests : FileStorageContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.CreateVersion7().ToString());

    protected override IFileStorage Storage => new LocalFileStorage(Options.Create(
        new StorageOptions { Provider = "Local", LocalRoot = _root }));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
