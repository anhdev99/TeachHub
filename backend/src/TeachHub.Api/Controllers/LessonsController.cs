using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Api.Filters;
using TeachHub.Application.Features.Content;
using TeachHub.Application.Features.Content.Commands.AddComponent;
using TeachHub.Application.Features.Content.Commands.ReorderLesson;
using TeachHub.Application.Features.Content.Queries.GetLesson;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/lessons")]
[Tags("Content")]
public sealed class LessonsController(ISender sender) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetLessonQuery(id, CurrentUserId), ct));

    [HttpPut("{id:guid}/reorder")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Lesson)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reorder(Guid id, ReorderLessonCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { LessonId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/components")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Lesson)]
    [ProducesResponseType<LessonComponentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddComponent(Guid id, AddComponentCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { LessonId = id, InstructorId = CurrentUserId }, ct));
}
