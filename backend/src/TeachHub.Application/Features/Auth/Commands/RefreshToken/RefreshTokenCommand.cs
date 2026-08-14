using FluentValidation;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<TokenPair>>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Thiếu refresh token.");
}

public sealed class RefreshTokenHandler(ITokenService tokens)
    : ICommandHandler<RefreshTokenCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var pair = await tokens.RotateAsync(request.RefreshToken, ct);

        return pair is null
            ? Result<TokenPair>.Unauthorized("Refresh token không hợp lệ, vui lòng đăng nhập lại.")
            : Result<TokenPair>.Ok(pair);
    }
}
