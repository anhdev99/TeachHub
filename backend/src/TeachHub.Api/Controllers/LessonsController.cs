using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Api.Filters;
using TeachHub.Application.Features.Content;
using TeachHub.Application.Features.Content.Commands.AddComponent;
using Microsoft.AspNetCore.RateLimiting;
using TeachHub.Application.Features.Content.Commands.ReorderLesson;
using TeachHub.Application.Features.Content.Commands.UploadLessonFile;
using TeachHub.Application.Features.Content.Queries.GetLesson;
using TeachHub.Application.Features.Learning.Commands.CompleteLesson;
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

    // KHÔNG gắn [RequireDraftVersion]: sinh viên học trên version đã published, filter đó
    // sẽ chặn sạch mọi request. Filter chỉ dành cho action sửa nội dung của giảng viên.
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, [FromQuery] int secondsSpent, CancellationToken ct)
        => ToActionResult(await sender.Send(new CompleteLessonCommand(id, CurrentUserId, secondsSpent), ct));

    [HttpPost("{id:guid}/file")]
    [Authorize(Policy = Policies.Instructor)]
    [RequireDraftVersion(RouteIdKind.Lesson)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200L * 1024 * 1024)]                              // hạn mức Kestrel
    [RequestFormLimits(MultipartBodyLengthLimit = 200L * 1024 * 1024)]  // hạn mức model binder multipart
    [EnableRateLimiting("upload")]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType<LessonFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadFile(
        Guid id, [FromForm] UploadLessonFileForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();

        return ToActionResult(await sender.Send(new UploadLessonFileCommand(
            form.File.FileName, form.File.Length, stream)
        {
            LessonId = id, InstructorId = CurrentUserId,
        }, ct));
    }
}

/// <summary>
/// Bọc IFormFile vào một lớp [FromForm] thay vì để tham số rời — bắt buộc, không phải thẩm mỹ.
/// Với tham số rời, /openapi/v1.json sinh content-type application/x-www-form-urlencoded và
/// schema liệt kê các thuộc tính NỘI BỘ của IFormFile (ContentType, Headers, Length, Name,
/// FileName…) — không có field nhị phân nào. Vì client TypeScript sinh từ chính tài liệu đó,
/// sai chỗ này là frontend không upload được, trong khi build và integration test viết tay
/// bằng MultipartFormDataContent vẫn xanh.
/// </summary>
public sealed class UploadLessonFileForm
{
    [FromForm(Name = "file")]
    public required IFormFile File { get; init; }
}
