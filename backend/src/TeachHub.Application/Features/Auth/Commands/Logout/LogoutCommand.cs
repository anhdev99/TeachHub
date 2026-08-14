using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Auth.Commands.Logout;

public sealed record LogoutCommand : ICommand<Result>
{
    public Guid UserId { get; init; }
}

public sealed class LogoutCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        // Chỉ thu hồi refresh token; access token còn hiệu lực tới khi hết hạn (tối đa 30 phút).
        // Thu hồi access token đòi một danh sách đen tra ở mọi request — cái giá đó không đáng
        // cho một hệ thống có access token sống ngắn như vậy.
        await db.RefreshTokens
            .Where(t => t.UserId == request.UserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);

        return Result.Ok();
    }
}
