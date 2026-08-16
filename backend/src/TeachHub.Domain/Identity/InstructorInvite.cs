using System.Security.Cryptography;
using System.Text;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

/// <summary>
/// An invitation to create an instructor account. The system does NOT offer public sign-up: an
/// LMS where anyone can register themselves as an instructor means anyone on the internet can
/// create an account and start uploading files — that is both a security hole and a storage
/// bill. The one exception is the very first person, via POST /api/auth/bootstrap.
/// </summary>
public class InstructorInvite : Entity
{
    public required string Email { get; set; }

    /// <summary>
    /// SHA-256 of the token; the real token is NOT stored — same reason as for RefreshToken:
    /// a database leak must not let anyone take over an account.
    /// </summary>
    public required string TokenHash { get; set; }

    public required Guid InvitedBy { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedUserId { get; set; }

    public bool IsUsable(DateTimeOffset now) => AcceptedAt is null && now < ExpiresAt;

    /// <summary>The real token: 32 random bytes, base64url-encoded, shown exactly once at creation.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
