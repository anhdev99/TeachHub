using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Api.Filters;
using TeachHub.Application.Features.Content;
using TeachHub.Application.Features.Content.Commands.CreateChapter;
using TeachHub.Application.Features.Content.Queries.GetChapters;
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

    [HttpGet("{id:guid}/chapters")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<ChapterDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChapters(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetChaptersQuery(id, CurrentUserId), ct));

    [HttpPost("{id:guid}/chapters")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Version)]
    [ProducesResponseType<ChapterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddChapter(Guid id, CreateChapterCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SubjectVersionId = id, InstructorId = CurrentUserId }, ct));
}
