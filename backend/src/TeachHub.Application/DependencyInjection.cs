using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Application.Common.Behaviors;
using TeachHub.Application.Features.Subjects;

namespace TeachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Thứ tự khai báo = thứ tự chạy, từ ngoài vào trong.
            // TransactionBehavior đăng ký ở Infrastructure, SAU hai cái này.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Cloner cố ý là lớp thường chứ không phải MediatR handler: nó là bước nhân bản cơ học,
        // Phase 4b sẽ dùng lại. Phần điều phối và kiểm quyền nằm ở command handler.
        services.AddScoped<SubjectVersionCloner>();

        return services;
    }
}
