using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Tests;

public class TokenExpiryTests
{
    [Fact]
    public void An_expired_token_is_no_longer_active()
    {
        // Using FakeTimeProvider instead of waiting 14 days — this is why every time-dependent
        // piece of logic in the project goes through TimeProvider rather than calling
        // DateTimeOffset.UtcNow.
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
    public void A_revoked_token_is_no_longer_active_even_before_it_expires()
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
