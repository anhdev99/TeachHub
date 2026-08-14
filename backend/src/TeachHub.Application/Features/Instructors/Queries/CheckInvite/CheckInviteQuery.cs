using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Instructors.Queries.CheckInvite;

/// <summary>
/// Chỉ trả đúng một cờ boolean và email đích. Không trả lý do không dùng được (hết hạn,
/// đã dùng, không tồn tại) — phân biệt được ba ca đó là dò được không gian token.
/// </summary>
public sealed record InviteStatusDto(bool Usable, string? Email);

public sealed record CheckInviteQuery(string Token) : IQuery<InviteStatusDto>;

public sealed class CheckInviteHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<CheckInviteQuery, InviteStatusDto>
{
    public async Task<InviteStatusDto> Handle(CheckInviteQuery request, CancellationToken ct)
    {
        var hash = InstructorInvite.HashToken(request.Token);
        var invite = await db.InstructorInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, ct);

        return invite is not null && invite.IsUsable(clock.GetUtcNow())
            ? new InviteStatusDto(true, invite.Email)
            : new InviteStatusDto(false, null);
    }
}
