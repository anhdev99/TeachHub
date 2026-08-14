using System.Security.Cryptography;
using System.Text;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

/// <summary>
/// Lời mời tạo tài khoản giảng viên. Hệ thống KHÔNG mở đăng ký công khai: một LMS mà ai
/// cũng tự đăng ký làm giảng viên nghĩa là bất kỳ ai trên internet cũng tạo được tài khoản
/// rồi upload file — vừa là lỗ hổng vừa là hoá đơn lưu trữ. Ngoại lệ duy nhất là người
/// đầu tiên, qua POST /api/auth/bootstrap.
/// </summary>
public class InstructorInvite : Entity
{
    public required string Email { get; set; }

    /// <summary>
    /// SHA-256 của token, KHÔNG lưu token thật — cùng lý do với RefreshToken:
    /// rò rỉ database không được phép cho ai chiếm tài khoản.
    /// </summary>
    public required string TokenHash { get; set; }

    public required Guid InvitedBy { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedUserId { get; set; }

    public bool IsUsable(DateTimeOffset now) => AcceptedAt is null && now < ExpiresAt;

    /// <summary>Token thật: 32 byte ngẫu nhiên, mã base64url, chỉ hiển thị một lần lúc tạo.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
