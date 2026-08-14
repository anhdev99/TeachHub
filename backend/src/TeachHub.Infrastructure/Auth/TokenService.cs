using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeachHub.Application.Common;
using TeachHub.Domain.Identity;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure.Auth;

public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Key { get; init; }
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}

public sealed class TokenService(
    AppDbContext db,
    UserManager<AppUser> users,
    IOptions<JwtOptions> options,
    TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public async Task<TokenPair> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new InvalidOperationException($"Không có user {userId}");
        var roles = await users.GetRolesAsync(user);
        var now = clock.GetUtcNow();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("full_name", user.FullName),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer, audience: _opt.Audience, claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_opt.AccessTokenMinutes).UtcDateTime,
            signingCredentials: creds);

        var refresh = GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_opt.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);

        return new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            refresh,
            _opt.AccessTokenMinutes * 60);
    }

    public async Task<TokenPair?> RotateAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null) return null;

        // Phát hiện tái sử dụng: token đã bị thu hồi mà vẫn được dùng ⇒ nhiều khả năng
        // đã rò rỉ. Thu hồi toàn bộ token của user đó và bắt đăng nhập lại.
        if (!stored.IsActive(now))
        {
            await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            return null;
        }

        stored.RevokedAt = now;
        return await IssueAsync(stored.UserId, ct);
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
