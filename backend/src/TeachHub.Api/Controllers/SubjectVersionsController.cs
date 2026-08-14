using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Subjects;
using TeachHub.Application.Features.Subjects.Commands.PublishVersion;
using TeachHub.Application.Features.Subjects.Commands.UpdateVersion;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/subject-versions")]
[Tags("SubjectVersions")]
public sealed class SubjectVersionsController(ISender sender) : ApiControllerBase
{
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubjectVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateVersionCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { VersionId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new PublishVersionCommand(id, CurrentUserId), ct));
}
