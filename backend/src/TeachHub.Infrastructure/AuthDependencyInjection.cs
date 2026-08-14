using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeachHub.Application.Common;
using TeachHub.Domain.Identity;
using TeachHub.Infrastructure.Auth;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Cấu hình tham số kiểm token qua IOptions<JwtOptions>, KHÔNG đọc thẳng
        // config["Jwt:Key"] ngay tại đây. Đọc thẳng là đọc NGAY lúc đăng ký service, trong khi
        // TokenService đọc IOptions lúc chạy — nên bất kỳ nguồn cấu hình nào nạp sau
        // (WebApplicationFactory trong test, biến môi trường, Key Vault) chỉ tới được một vế.
        // Hậu quả đã gặp thật: test ký token bằng issuer "teachhub-test" còn JwtBearer kiểm
        // bằng "teachhub" của appsettings.json → mọi request có token đều 401, không log gì.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var opt = jwt.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,   ValidIssuer   = opt.Issuer,
                    ValidateAudience = true, ValidAudience = opt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            // Bảo vệ mặc định: endpoint nào KHÔNG khai báo gì về authorization thì vẫn
            // đòi đăng nhập. Muốn mở công khai phải gắn [AllowAnonymous] tường minh —
            // nhờ vậy "quên bảo vệ endpoint" là lỗi không thể xảy ra do đãng trí,
            // chỉ có thể xảy ra do ai đó cố ý gõ thêm một dòng.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Instructor, p => p.RequireRole(Roles.Instructor))
            .AddPolicy(Policies.Student,    p => p.RequireRole(Roles.Student));

        services.AddScoped<ITokenService, TokenService>();
        return services;
    }
}

public static class Policies
{
    public const string Instructor = nameof(Instructor);

    // KHÔNG có InstructorOrTa. Vai trò TeachingAssistant vẫn tồn tại trong Roles.All để sau này
    // bật lại không phải migrate, nhưng CHƯA CÓ POLICY NÀO cấp quyền cho nó — xem ADR-013.
    // Lý do: không có bảng gắn trợ giảng vào một lớp cụ thể, mà mọi kiểm quyền viết dạng
    // subject.InstructorId == currentUserId. Nên hoặc trợ giảng không làm được gì, hoặc
    // — nếu nới điều kiện — làm được ở MỌI lớp của MỌI giảng viên. Không có mức trung gian.
    public const string Student = nameof(Student);
}
