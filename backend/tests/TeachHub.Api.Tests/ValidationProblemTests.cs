using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Api.Common;

namespace TeachHub.Api.Tests;

public class ValidationProblemTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Validation_exception_thanh_400_kem_danh_sach_loi_theo_tung_truong()
    {
        var sp = BuildServices();
        var ctx = new DefaultHttpContext { RequestServices = sp };
        ctx.Response.Body = new MemoryStream();

        var handler = new ValidationExceptionHandler(sp.GetRequiredService<IProblemDetailsService>());
        var ex = new ValidationException([new ValidationFailure("Email", "Email không hợp lệ")]);

        (await handler.TryHandleAsync(ctx, ex, CancellationToken.None)).ShouldBeTrue();

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ctx.Response.ContentType.ShouldBe("application/problem+json");

        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.ShouldContain("\"errors\"");
        body.ShouldContain("Email không hợp lệ");
    }

    [Fact]
    public async Task Exception_khac_khong_bi_nuot_thanh_400()
    {
        // Nếu handler trả true cho mọi exception thì một NullReferenceException cũng
        // biến thành 400 và lỗi thật sẽ không bao giờ lộ ra trong log giám sát.
        var sp = BuildServices();
        var handler = new ValidationExceptionHandler(sp.GetRequiredService<IProblemDetailsService>());

        var xuLy = await handler.TryHandleAsync(
            new DefaultHttpContext { RequestServices = sp },
            new InvalidOperationException("lỗi thật"),
            CancellationToken.None);

        xuLy.ShouldBeFalse();
    }
}
