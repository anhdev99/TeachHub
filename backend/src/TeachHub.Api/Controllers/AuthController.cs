using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeachHub.Application.Common;
using TeachHub.Application.Features.Auth.Commands.Login;
using TeachHub.Application.Features.Auth.Commands.RefreshToken;
using TeachHub.Application.Features.Auth.Queries.GetMe;
using TeachHub.Application.Features.Instructors.Commands.AcceptInvite;
using TeachHub.Application.Features.Instructors.Commands.Bootstrap;
using TeachHub.Application.Features.Instructors.Queries.CheckInvite;

namespace TeachHub.Api.Controllers;

[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<TokenPair>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<TokenPair>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command, ct));

    /// <summary>Kiểm token lời mời còn dùng được không — trang đăng ký gọi trước khi hiện form.</summary>
    [HttpGet("invites/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<InviteStatusDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckInvite(string token, CancellationToken ct)
        => Ok(await sender.Send(new CheckInviteQuery(token), ct));

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(AcceptInviteCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command, ct));

    [HttpPost("bootstrap")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Bootstrap(BootstrapInstructorCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command, ct));

    // Không dùng ToActionResult vì GetMeQuery trả MeDto? chứ không phải Result —
    // query "không tìm thấy" không phải kết quả nghiệp vụ cần diễn giải, chỉ là null.
    [HttpGet("me")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
        => await sender.Send(new GetMeQuery(CurrentUserId), ct) is { } me
            ? Ok(me)
            : NotFound();
}
