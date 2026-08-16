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

            // Declaration order = execution order, outermost first.
            // TransactionBehavior is registered in Infrastructure, AFTER these two.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // The cloner is deliberately a plain class rather than a MediatR handler: it is the
        // mechanical copying step, and Phase 4b reuses it. Orchestration and permission checks
        // live in the command handler.
        services.AddScoped<SubjectVersionCloner>();

        return services;
    }
}
