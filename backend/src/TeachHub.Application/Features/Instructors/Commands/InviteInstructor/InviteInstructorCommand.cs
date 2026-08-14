using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Instructors.Commands.InviteInstructor;

/// <summary>Token chỉ xuất hiện trong DTO này, đúng một lần lúc tạo. Không có API nào đọc lại được.</summary>
public sealed record InviteCreatedDto(Guid Id, string Email, string Token, DateTimeOffset ExpiresAt);

public sealed record InviteInstructorCommand(string Email) : ICommand<Result<InviteCreatedDto>>
{
    public Guid InvitedBy { get; init; }
}

public sealed class InviteInstructorValidator : AbstractValidator<InviteInstructorCommand>
{
    public InviteInstructorValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class InviteInstructorHandler(
    IAppDbContext db,
    UserManager<AppUser> users,
    TimeProvider clock) : ICommandHandler<InviteInstructorCommand, Result<InviteCreatedDto>>
{
    private const int SoNgayHieuLuc = 7;

    public async Task<Result<InviteCreatedDto>> Handle(InviteInstructorCommand request, CancellationToken ct)
    {
        // Nói thẳng lý do được: người mời là giảng viên đã đăng nhập, đã được tin tưởng.
        // Khác hẳn luồng quên-mật-khẩu, nơi phải mơ hồ để không lộ email nào tồn tại.
        if (await users.FindByEmailAsync(request.Email) is not null)
            return Result<InviteCreatedDto>.Conflict("Email này đã có tài khoản trong hệ thống.");

        var now = clock.GetUtcNow();

        // Lời mời cũ chưa dùng cho cùng email thì thu hồi, tránh tồn tại hai token sống song song.
        await db.InstructorInvites
            .Where(i => i.Email == request.Email && i.AcceptedAt == null && i.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, now), ct);

        var token = InstructorInvite.NewToken();
        var invite = new InstructorInvite
        {
            Email = request.Email,
            TokenHash = InstructorInvite.HashToken(token),
            InvitedBy = request.InvitedBy,
            CreatedAt = now,
            ExpiresAt = now.AddDays(SoNgayHieuLuc),
        };

        db.InstructorInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        return Result<InviteCreatedDto>.Ok(new InviteCreatedDto(invite.Id, invite.Email, token, invite.ExpiresAt));
    }
}
