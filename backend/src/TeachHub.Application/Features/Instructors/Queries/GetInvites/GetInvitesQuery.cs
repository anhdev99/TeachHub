using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Instructors.Queries.GetInvites;

/// <summary>Cố ý KHÔNG có trường Token — token chỉ hiện đúng một lần lúc tạo lời mời.</summary>
public sealed record InviteDto(
    Guid Id, string Email, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt);

public sealed record GetInvitesQuery : IQuery<IReadOnlyList<InviteDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class GetInvitesHandler(IAppDbContext db)
    : IQueryHandler<GetInvitesQuery, IReadOnlyList<InviteDto>>
{
    public async Task<IReadOnlyList<InviteDto>> Handle(GetInvitesQuery request, CancellationToken ct) =>
        // Lọc InvitedBy NGAY trong câu truy vấn, không nạp rồi lọc trong bộ nhớ:
        // một dòng .Where() bị quên khi copy sang handler kế tiếp là lộ hết.
        await db.InstructorInvites
            .AsNoTracking()
            .Where(i => i.InvitedBy == request.InstructorId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteDto(i.Id, i.Email, i.CreatedAt, i.ExpiresAt, i.AcceptedAt))
            .ToListAsync(ct);
}
