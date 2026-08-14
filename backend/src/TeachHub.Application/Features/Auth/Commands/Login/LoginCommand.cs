using FluentValidation;
using Microsoft.AspNetCore.Identity;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<TokenPair>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        // Thông báo lỗi trả cho người dùng viết tiếng Việt (quy ước dự án). Thông báo mặc
        // định của FluentValidation là tiếng Anh và thư viện không có gói ngôn ngữ tiếng Việt,
        // nên phải khai tường minh ở từng luật.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Chưa nhập email.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Chưa nhập mật khẩu.");
    }
}

public sealed class LoginHandler(UserManager<AppUser> users, ITokenService tokens)
    : ICommandHandler<LoginCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);

        // Một thông báo duy nhất cho cả hai trường hợp. Tách ra thành "email không tồn tại"
        // và "sai mật khẩu" là biếu không cho kẻ tấn công một công cụ dò danh sách email.
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            return Result<TokenPair>.Unauthorized("Email hoặc mật khẩu không đúng.");

        return Result<TokenPair>.Ok(await tokens.IssueAsync(user.Id, ct));
    }
}
