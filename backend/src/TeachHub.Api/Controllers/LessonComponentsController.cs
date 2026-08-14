using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Api.Filters;
using TeachHub.Application.Features.Content.Commands.DeleteComponent;
using TeachHub.Application.Features.Content.Commands.ReorderComponent;
using TeachHub.Application.Features.Learning.Commands.CompleteComponent;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/lesson-components")]
[Tags("Content")]
public sealed class LessonComponentsController(ISender sender) : ApiControllerBase
{
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Component)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new DeleteComponentCommand { ComponentId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new CompleteComponentCommand(id, CurrentUserId), ct));

    [HttpPut("{id:guid}/reorder")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Component)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reorder(Guid id, ReorderComponentCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { ComponentId = id, InstructorId = CurrentUserId }, ct));
}
