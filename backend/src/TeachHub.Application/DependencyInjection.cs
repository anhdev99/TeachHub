using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Application.Common.Behaviors;

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
        return services;
    }
}
