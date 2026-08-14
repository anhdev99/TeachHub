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

        // Gom TOÀN BỘ lỗi rồi mới ném một lần, không dừng ở lỗi đầu tiên: form nhập điểm
        // có 12 ô, bắt người dùng sửa từng ô qua 12 lần round-trip là cách chắc chắn để
        // họ ghét hệ thống.
        var failures = results.SelectMany(r => r.Errors).ToList();

        if (failures.Count != 0) throw new ValidationException(failures);

        return await next();
    }
}
