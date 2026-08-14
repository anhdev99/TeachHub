using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Instructors.Commands.RevokeInvite;

public sealed record RevokeInviteCommand : ICommand<Result>
{
    public Guid InviteId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class RevokeInviteHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<RevokeInviteCommand, Result>
{
    public async Task<Result> Handle(RevokeInviteCommand request, CancellationToken ct)
    {
        // Điều kiện sở hữu nằm NGAY trong câu truy vấn. Lời mời của giảng viên khác không
        // tồn tại đối với người gọi — trả 404, không bao giờ 403: 403 là xác nhận id có thật,
        // đủ để dò ra cấu trúc dữ liệu của đồng nghiệp chỉ bằng cách thử id (ADR-012).
        var invite = await db.InstructorInvites.SingleOrDefaultAsync(
            i => i.Id == request.InviteId && i.InvitedBy == request.InstructorId, ct);

        if (invite is null) return Result.NotFound("Không tìm thấy lời mời.");

        invite.ExpiresAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
