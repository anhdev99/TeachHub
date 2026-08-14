using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Result>
{
    public Guid UserId { get; init; }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Chưa nhập mật khẩu hiện tại.");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Chưa nhập mật khẩu mới.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

public sealed class ChangePasswordCommandHandler(
    IAppDbContext db,
    UserManager<AppUser> users,
    TimeProvider clock) : ICommandHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) return Result.NotFound("Không tìm thấy tài khoản.");

        var kq = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!kq.Succeeded)
            return Result.Invalid(string.Join(" ", kq.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await users.UpdateAsync(user);

        // Thu hồi TOÀN BỘ refresh token: đổi mật khẩu mà phiên cũ vẫn sống thì việc đổi
        // gần như vô nghĩa — người đang chiếm tài khoản vẫn ở nguyên trong đó.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);

        return Result.Ok();
    }
}
