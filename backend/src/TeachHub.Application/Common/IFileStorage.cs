namespace TeachHub.Application.Common;

public sealed record StoredFile(string Path, long SizeBytes, string ContentType);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(string container, string fileName, Stream content,
                               string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);

    /// <summary>URL tạm để trình duyệt tải trực tiếp, không đi qua API.</summary>
    Task<Uri> GetPresignedUrlAsync(string path, TimeSpan lifetime, CancellationToken ct);
}
