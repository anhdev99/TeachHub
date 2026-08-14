using FluentValidation;
using Microsoft.AspNetCore.Identity;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Instructors.Commands.Bootstrap;

/// <summary>
/// Ngoại lệ duy nhất của luật "chỉ tạo tài khoản qua lời mời": người đầu tiên của hệ thống.
/// Dùng đúng một lần trong đời hệ thống.
/// </summary>
public sealed record BootstrapInstructorCommand(string Email, string FullName, string Password)
    : ICommand<Result>;

public sealed class BootstrapInstructorValidator : AbstractValidator<BootstrapInstructorCommand>
{
    public BootstrapInstructorValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class BootstrapInstructorHandler(UserManager<AppUser> users, RoleManager<AppRole> roles)
    : ICommandHandler<BootstrapInstructorCommand, Result>
{
    public async Task<Result> Handle(BootstrapInstructorCommand request, CancellationToken ct)
    {
        // Kiểm "đã có giảng viên nào chưa", KHÔNG kiểm "có tài khoản nào đang hoạt động không".
        // Nếu nới thành điều kiện thứ hai thì vô hiệu hoá giảng viên đầu tiên là mở lại cánh
        // cửa này cho bất kỳ ai biết đường dẫn — đúng thứ mà việc bỏ đăng ký công khai muốn tránh.
        if (!await roles.RoleExistsAsync(Roles.Instructor))
            await roles.CreateAsync(new AppRole(Roles.Instructor));

        var daCo = await users.GetUsersInRoleAsync(Roles.Instructor);
        if (daCo.Count > 0)
            return Result.Conflict("Hệ thống đã có giảng viên. Hãy yêu cầu một lời mời.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
        };

        var tao = await users.CreateAsync(user, request.Password);
        if (!tao.Succeeded)
            return Result.Invalid(string.Join(" ", tao.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, Roles.Instructor);
        return Result.Ok();
    }
}
