using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TeachHub.Infrastructure.Jobs;

public static class JobsDependencyInjection
{
    /// <summary>Hàng đợi riêng cho việc giải nén ZIP — xem ADR-005.</summary>
    public const string ZipQueue = "zip";

    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue("Hangfire:Enabled", true)) return services;

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
