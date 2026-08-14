using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Tests;

public class TokenExpiryTests
{
    [Fact]
    public void Token_het_han_thi_khong_con_active()
    {
        // Dùng FakeTimeProvider thay vì chờ 14 ngày — đây là lý do mọi logic phụ thuộc
        // thời gian trong dự án phải đi qua TimeProvider chứ không gọi DateTimeOffset.UtcNow.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero));
        var token = new RefreshToken
        {
            UserId = Guid.CreateVersion7(),
            TokenHash = "x",
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = clock.GetUtcNow().AddDays(14),
        };

        token.IsActive(clock.GetUtcNow()).ShouldBeTrue();

        clock.Advance(TimeSpan.FromDays(14) + TimeSpan.FromSeconds(1));
        token.IsActive(clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Token_da_thu_hoi_thi_khong_con_active_du_chua_het_han()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero));
        var token = new RefreshToken
        {
            UserId = Guid.CreateVersion7(),
            TokenHash = "x",
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = clock.GetUtcNow().AddDays(14),
            RevokedAt = clock.GetUtcNow(),
        };

        token.IsActive(clock.GetUtcNow()).ShouldBeFalse();
    }
}
