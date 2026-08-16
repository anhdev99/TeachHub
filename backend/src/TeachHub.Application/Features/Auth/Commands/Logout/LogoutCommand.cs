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
        // Only the refresh tokens are revoked; an access token stays valid until it expires
        // (30 minutes at most). Revoking access tokens would require a blacklist consulted on
        // every request — a price not worth paying for a system whose access tokens are this
        // short-lived.
        await db.RefreshTokens
            .Where(t => t.UserId == request.UserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);

        return Result.Ok();
    }
}
