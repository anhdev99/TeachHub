using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TeachHub.Application.Common;

namespace TeachHub.Infrastructure.Storage;

/// <summary>
/// Adapter S3, dùng được với cả MinIO nhờ ForcePathStyle. Phải qua đúng bộ
/// FileStorageContractTests như LocalFileStorage — đổi provider lúc lên production mà
/// hai adapter cư xử khác nhau thì thứ mất là bài nộp của sinh viên.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var opt = options.Value;
        _bucket = opt.Bucket ?? throw new InvalidOperationException(
            "Thiếu Storage:Bucket — bắt buộc khi Storage:Provider = S3.");

        var config = new AmazonS3Config
        {
            ForcePathStyle = true,           // MinIO không hỗ trợ virtual-host style
            ServiceURL = opt.ServiceUrl,
        };

        _s3 = opt.AccessKey is not null && opt.SecretKey is not null
            ? new AmazonS3Client(opt.AccessKey, opt.SecretKey, config)
            : new AmazonS3Client(config);
    }

    public async Task<StoredFile> SaveAsync(string container, string fileName, Stream content,
                                            string contentType, CancellationToken ct)
    {
        // Cùng luật với LocalFileStorage: tên do người dùng gửi lên chỉ giữ phần mở rộng.
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 16 || ext.Any(c => Path.GetInvalidFileNameChars().Contains(c))) ext = "";

        var key = $"{Sanitize(container)}/{Guid.CreateVersion7()}{ext}";

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        // Lấy kích thước TRƯỚC khi gửi: AWS SDK đóng InputStream sau PutObjectAsync,
        // nên đọc buffer.Length sau lời gọi sẽ ném ObjectDisposedException.
        var size = buffer.Length;

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = key, InputStream = buffer, ContentType = contentType,
        }, ct);

        return new StoredFile(key, size, contentType);
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        Guard(path);
        try
        {
            var res = await _s3.GetObjectAsync(_bucket, path, ct);
            return res.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Quy về cùng một loại exception với LocalFileStorage — nếu hai adapter ném
            // hai kiểu khác nhau thì code gọi phải biết đang chạy trên provider nào.
            throw new FileNotFoundException(path, ex);
        }
    }

    public async Task DeleteAsync(string path, CancellationToken ct)
    {
        Guard(path);
        await _s3.DeleteObjectAsync(_bucket, path, ct);
    }

    public Task<Uri> GetPresignedUrlAsync(string path, TimeSpan lifetime, CancellationToken ct)
    {
        Guard(path);
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = path,
            Expires = DateTime.UtcNow.Add(lifetime),
        });
        return Task.FromResult(new Uri(url));
    }

    private static void Guard(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal) || path.StartsWith('/'))
            throw new UnauthorizedAccessException($"Đường dẫn thoát khỏi thư mục lưu trữ: {path}");
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
}
