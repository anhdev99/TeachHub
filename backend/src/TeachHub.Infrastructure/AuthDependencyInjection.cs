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

        // Configure the token validation parameters through IOptions<JwtOptions>, do NOT read
        // config["Jwt:Key"] straight from here. Reading it straight reads it AT SERVICE
        // REGISTRATION TIME, while TokenService reads IOptions at run time — so any configuration
        // source loaded later (WebApplicationFactory in tests, environment variables, Key Vault)
        // only ever reaches one of the two sides. This actually happened: the tests signed tokens
        // with issuer "teachhub-test" while JwtBearer validated against "teachhub" from
        // appsettings.json → every request carrying a token got a 401, with nothing in the log.
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
            // Secure by default: any endpoint that declares NOTHING about authorization still
            // demands a signed-in user. Making one public takes an explicit [AllowAnonymous] —
            // which turns "forgot to protect an endpoint" into a mistake that cannot happen
            // through absent-mindedness, only through someone deliberately typing one more line.
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

    // There is NO InstructorOrTa. The TeachingAssistant role still exists in Roles.All so that
    // turning it back on later needs no migration, but NO POLICY grants it anything yet — see
    // ADR-013. The reason: there is no table binding a teaching assistant to one specific class,
    // while every permission check is written as subject.InstructorId == currentUserId. So either
    // the assistant can do nothing, or — if the condition is loosened — they can act on EVERY
    // class of EVERY instructor. There is no middle ground.
    public const string Student = nameof(Student);
}
