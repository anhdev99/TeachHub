using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TeachHub.Api.Common;

public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        // Trả false cho mọi exception khác: handler này chỉ nhận đúng phần việc của nó,
        // phần còn lại để pipeline xử lý lỗi mặc định lo (và trả 500).
        if (exception is not ValidationException ex) return false;

        var errors = ex.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = ctx,
            Exception      = exception,
            ProblemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title  = "Dữ liệu gửi lên không hợp lệ",
            },
        });
    }
}
