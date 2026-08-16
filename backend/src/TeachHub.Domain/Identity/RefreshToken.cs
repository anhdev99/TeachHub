using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

/// <summary>
/// Rotating refresh token: every use revokes it and issues a new one.
/// Stores the SHA-256, not the real token — a DB leak must not enable impersonation.
/// </summary>
public class RefreshToken : Entity
{
    public required Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
