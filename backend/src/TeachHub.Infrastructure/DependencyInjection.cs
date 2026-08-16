using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Application.Common;
using TeachHub.Infrastructure.Behaviors;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(config.GetConnectionString("Default"),
                npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IEnrollmentReader, Excel.ClosedXmlEnrollmentReader>();
        services.AddScoped<Application.Features.Gradebook.IGradebookExcelWriter, Excel.ClosedXmlGradebookWriter>();

        // Đăng ký SAU Logging và Validation (khai trong AddApplication) — thứ tự đăng ký
        // chính là thứ tự chạy từ ngoài vào trong, nên Transaction phải là lớp trong cùng,
        // sát handler nhất. Đảo lên trước Validation thì mỗi request sai dữ liệu vẫn mở
        // một transaction rồi rollback, và không test HTTP nào bắt được vì kết quả vẫn 400.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
