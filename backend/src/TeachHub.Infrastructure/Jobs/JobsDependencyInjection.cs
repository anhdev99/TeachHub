using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Application.Common;

namespace TeachHub.Infrastructure.Jobs;

public static class JobsDependencyInjection
{
    /// <summary>Hàng đợi riêng cho việc giải nén ZIP — xem ADR-005.</summary>
    public const string ZipQueue = "zip";

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ProcessSubmissionFileJob>();
        services.AddScoped<ComputeFingerprintsJob>();     // Phase 6
        services.AddScoped<ReviewQueueJob>();             // Phase 8

        if (!config.GetValue("Hangfire:Enabled", true))
        {
            // Hangfire tắt (integration test, môi trường tối giản): xử lý ngay trong tiến trình
            // thay vì bỏ qua. Bỏ qua thì tệp nộp nằm mãi ở trạng thái Pending và cây thư mục
            // không bao giờ hiện ra — một chế độ hỏng âm thầm, khó lần hơn hẳn so với chậm.
            services.AddScoped<ISubmissionFileQueue, InlineSubmissionFileQueue>();
            services.AddScoped<ISimilarityQueue, InlineSimilarityQueue>();
            return services;
        }

        services.AddScoped<ISubmissionFileQueue, HangfireSubmissionFileQueue>();
        services.AddScoped<ISimilarityQueue, HangfireSimilarityQueue>();

        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(config.GetConnectionString("Default"))));

        services.AddHangfireServer(o =>
        {
            // Worker unzip ít và tách riêng: một ZIP độc hại không được phép
            // chiếm hết worker và làm treo mọi job khác.
            o.Queues = ["default", ZipQueue];
            o.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 8);
        });

        return services;
    }
}
