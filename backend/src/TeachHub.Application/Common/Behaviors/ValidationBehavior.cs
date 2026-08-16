using FluentValidation;
using MediatR;

namespace TeachHub.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));

        // Collect EVERY failure and throw once, rather than stopping at the first one: the grade
        // entry form has 12 fields, and making the user fix them one at a time across 12
        // round-trips is a reliable way to make them hate the system.
        var failures = results.SelectMany(r => r.Errors).ToList();

        if (failures.Count != 0) throw new ValidationException(failures);

        return await next();
    }
}
