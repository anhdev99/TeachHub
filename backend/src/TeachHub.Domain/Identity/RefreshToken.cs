using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

/// <summary>
/// Refresh token xoay vòng: mỗi lần dùng sẽ bị thu hồi và phát hành token mới.
/// Lưu SHA-256 chứ không lưu token thật — rò rỉ DB không cho phép mạo danh.
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
