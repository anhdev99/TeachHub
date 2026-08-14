using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Domain.Identity;

namespace TeachHub.Api.Middleware;

/// <summary>
/// Chặn mọi request của người dùng còn cờ <c>MustChangePassword</c>, trừ đúng hai đường thoát.
///
/// Là MIDDLEWARE chứ không phải pipeline behavior của MediatR: nó phải chặn được cả những
/// request chưa từng đi tới handler nào (endpoint chỉ đọc, route sai, tài nguyên tĩnh).
/// Behavior chỉ chạy khi đã có một request MediatR — quá muộn và quá hẹp.
///
/// Hai đường thoát PHẢI tồn tại trước khi bật middleware này, nếu không sinh viên đăng nhập
/// lần đầu bị chặn ở mọi endpoint kể cả đường để tự gỡ chặn, và không có cách nào vào hệ thống.
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly string[] DuongThoat =
    [
        "/api/auth/change-password",
        "/api/auth/logout",
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/me",
        "/health",
        "/openapi",
        "/scalar",
    ];

    public async Task InvokeAsync(HttpContext ctx, UserManager<AppUser> users)
    {
        if (ctx.User.Identity?.IsAuthenticated is not true)
        {
            await next(ctx);
            return;
        }

        var duong = ctx.Request.Path.Value ?? "";
        if (DuongThoat.Any(d => duong.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            await next(ctx);
            return;
        }

        var id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await users.FindByIdAsync(id);

        if (user?.MustChangePassword is true)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/problem+json";

            // Mã máy đọc được nằm trong extensions để frontend biết mà chuyển hướng sang
            // trang đổi mật khẩu, thay vì hiện một lỗi 403 chung chung.
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Cần đổi mật khẩu",
                Detail = "Bạn phải đổi mật khẩu lần đầu trước khi dùng hệ thống.",
                Extensions = { ["code"] = "must_change_password" },
            });
            return;
        }

        await next(ctx);
    }
}
