using Microsoft.Extensions.Options;
using TeachHub.Application.Common;

namespace TeachHub.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Provider { get; init; } = "Local";
    public string LocalRoot { get; init; } = "./uploads";
    public string? Bucket { get; init; }
    public string? ServiceUrl { get; init; }
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
}

public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRoot);

    public async Task<StoredFile> SaveAsync(string container, string fileName, Stream content,
                                            string contentType, CancellationToken ct)
    {
        // Không bao giờ tin tên file do người dùng gửi lên. Chỉ giữ phần mở rộng,
        // tên thật thay bằng UUIDv7 — chặn cả path traversal lẫn trùng tên.
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 16 || ext.Any(c => Path.GetInvalidFileNameChars().Contains(c))) ext = "";

        var relative = $"{Sanitize(container)}/{Guid.CreateVersion7()}{ext}";
        var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);

        return new StoredFile(relative, fs.Length, contentType);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        var full = Resolve(path);
        if (!File.Exists(full)) throw new FileNotFoundException(path);
        return Task.FromResult<Stream>(File.OpenRead(full));
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        File.Delete(Resolve(path));
        return Task.CompletedTask;
    }

    public Task<Uri> GetPresignedUrlAsync(string path, TimeSpan lifetime, CancellationToken ct) =>
        // Chế độ Local không có presigned URL: API sẽ stream file qua endpoint có kiểm quyền.
        Task.FromResult(new Uri($"/api/files/{Uri.EscapeDataString(path)}", UriKind.Relative));

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        // Chốt chặn cuối: đường dẫn kết quả BẮT BUỘC nằm trong _root.
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Đường dẫn thoát khỏi thư mục lưu trữ: {relative}");
        return full;
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
}
