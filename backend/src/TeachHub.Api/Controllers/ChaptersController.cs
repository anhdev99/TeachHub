using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Api.Filters;
using TeachHub.Application.Features.Content;
using TeachHub.Application.Features.Content.Commands.CreateLesson;
using TeachHub.Application.Features.Content.Commands.DeleteChapter;
using TeachHub.Application.Features.Content.Queries.GetLessons;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/chapters")]
[Tags("Content")]
public sealed class ChaptersController(ISender sender) : ApiControllerBase
{
    [HttpGet("{id:guid}/lessons")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<LessonDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLessons(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetLessonsQuery(id, CurrentUserId), ct));

    [HttpPost("{id:guid}/lessons")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Chapter)]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddLesson(Guid id, CreateLessonCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { ChapterId = id, InstructorId = CurrentUserId }, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Chapter)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new DeleteChapterCommand { ChapterId = id, InstructorId = CurrentUserId }, ct));
}
