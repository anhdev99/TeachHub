using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TeachHub.Application.Common;

namespace TeachHub.Infrastructure.Storage;

public static class StorageDependencyInjection
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<StorageOptions>(config.GetSection("Storage"));

        services.AddKeyedScoped<IFileStorage, LocalFileStorage>("Local");
        services.AddKeyedScoped<IFileStorage, S3FileStorage>("S3");

        // Đọc Provider qua IOptions lúc phân giải service, không đọc thẳng config lúc đăng ký:
        // cùng lý do với JwtOptions ở AuthDependencyInjection — nguồn cấu hình nạp sau
        // (test, biến môi trường) phải tới được cả nơi chọn provider.
        services.AddScoped<IFileStorage>(sp =>
            sp.GetRequiredKeyedService<IFileStorage>(
                sp.GetRequiredService<IOptions<StorageOptions>>().Value.Provider));

        return services;
    }
}
