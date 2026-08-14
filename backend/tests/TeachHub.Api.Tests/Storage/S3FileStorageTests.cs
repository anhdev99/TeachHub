using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TeachHub.Application.Common;
using TeachHub.Infrastructure.Storage;
using Testcontainers.Minio;

namespace TeachHub.Api.Tests.Storage;

/// <summary>
/// Chạy ĐÚNG bộ FileStorageContractTests như LocalFileStorage, nhưng trên MinIO thật trong
/// container. Đây là điểm mấu chốt của Task 0.10: nếu chỉ test adapter Local thì ngày đổi
/// Storage:Provider sang S3 lúc lên production, mọi khác biệt hành vi mới lộ ra — trên
/// dữ liệu thật.
/// </summary>
public sealed class S3FileStorageTests : FileStorageContractTests, IAsyncLifetime
{
    private const string Bucket = "teachhub-test";
    private const string User = "teachhub";
    private const string Password = "teachhub_dev_secret";

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:latest")
        .WithUsername(User)
        .WithPassword(Password)
        .Build();

    private S3FileStorage _storage = null!;
    protected override IFileStorage Storage => _storage;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();

        var options = Options.Create(new StorageOptions
        {
            Provider = "S3",
            Bucket = Bucket,
            ServiceUrl = $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}",
            AccessKey = User,
            SecretKey = Password,
        });

        using var client = new AmazonS3Client(User, Password, new AmazonS3Config
        {
            ForcePathStyle = true,
            ServiceURL = options.Value.ServiceUrl,
        });
        await client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });

        _storage = new S3FileStorage(options);
    }

    public async Task DisposeAsync() => await _minio.DisposeAsync();
}
