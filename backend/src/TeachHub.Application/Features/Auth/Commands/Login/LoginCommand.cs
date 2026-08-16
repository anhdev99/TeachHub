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
        // Error messages returned to the user are written in Vietnamese (project convention).
        // FluentValidation's default messages are English and the library ships no Vietnamese
        // language pack, so every rule has to spell its message out explicitly.
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

        // A single message for both cases. Splitting it into "this email does not exist" and
        // "wrong password" hands an attacker a free tool for enumerating the list of emails.
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            return Result<TokenPair>.Unauthorized("Email hoặc mật khẩu không đúng.");

        return Result<TokenPair>.Ok(await tokens.IssueAsync(user.Id, ct));
    }
}
