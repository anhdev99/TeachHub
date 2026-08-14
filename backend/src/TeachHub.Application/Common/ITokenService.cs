namespace TeachHub.Application.Common;

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

public interface ITokenService
{
    Task<TokenPair> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Xoay vòng: thu hồi token cũ, phát hành cặp mới. Null nếu token không hợp lệ.</summary>
    Task<TokenPair?> RotateAsync(string refreshToken, CancellationToken ct = default);
}
