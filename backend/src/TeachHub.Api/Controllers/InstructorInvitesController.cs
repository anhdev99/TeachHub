using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Instructors.Commands.InviteInstructor;
using TeachHub.Application.Features.Instructors.Commands.RevokeInvite;
using TeachHub.Application.Features.Instructors.Queries.GetInvites;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/instructor/invites")]
[Tags("InstructorInvites")]
public sealed class InstructorInvitesController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<InviteCreatedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Invite(InviteInstructorCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InvitedBy = CurrentUserId }, ct));

    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<InviteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await sender.Send(new GetInvitesQuery { InstructorId = CurrentUserId }, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new RevokeInviteCommand { InviteId = id, InstructorId = CurrentUserId }, ct));
}
