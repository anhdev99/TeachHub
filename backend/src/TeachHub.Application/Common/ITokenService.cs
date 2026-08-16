namespace TeachHub.Application.Common;

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

public interface ITokenService
{
    Task<TokenPair> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Rotation: revoke the old token, issue a new pair. Null when the token is invalid.</summary>
    Task<TokenPair?> RotateAsync(string refreshToken, CancellationToken ct = default);
}
