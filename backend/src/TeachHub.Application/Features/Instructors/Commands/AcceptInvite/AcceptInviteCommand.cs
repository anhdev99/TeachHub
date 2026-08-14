using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Instructors.Commands.AcceptInvite;

public sealed record AcceptInviteCommand(string Token, string FullName, string Password) : ICommand<Result>;

public sealed class AcceptInviteValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Thiếu mã lời mời.");
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Chưa nhập họ tên.")
            .MaximumLength(256).WithMessage("Họ tên dài quá 256 ký tự.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Chưa nhập mật khẩu.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.");
    }
}

public sealed class AcceptInviteHandler(
    IAppDbContext db,
    UserManager<AppUser> users,
    TimeProvider clock) : ICommandHandler<AcceptInviteCommand, Result>
{
    // MỘT thông báo duy nhất cho token bịa, token đã dùng và token hết hạn. Khác nhau một chữ
    // là kẻ tấn công phân biệt được "token này từng tồn tại" với "token bịa", tức dò được
    // không gian token bằng cách thử.
    private const string LoiChung = "Lời mời không hợp lệ hoặc đã hết hạn.";

    public async Task<Result> Handle(AcceptInviteCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = InstructorInvite.HashToken(request.Token);

        var invite = await db.InstructorInvites.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invite is null || !invite.IsUsable(now)) return Result.Invalid(LoiChung);

        if (await users.FindByEmailAsync(invite.Email) is not null)
            return Result.Invalid(LoiChung);

        var user = new AppUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
        };

        var tao = await users.CreateAsync(user, request.Password);
        if (!tao.Succeeded)
            return Result.Invalid(string.Join(" ", tao.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, Roles.Instructor);

        invite.AcceptedAt = now;
        invite.AcceptedUserId = user.Id;
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
