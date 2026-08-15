using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeachHub.Application.Features.Assignments;
using TeachHub.Application.Features.Assignments.Commands.CreateAssignment;
using TeachHub.Application.Features.Assignments.Commands.ManageSlots;
using TeachHub.Application.Features.Assignments.Commands.UpdateAssignment;
using TeachHub.Application.Features.Assignments.Queries.GetAssignments;
using TeachHub.Application.Features.Submissions;
using TeachHub.Application.Features.Submissions.Commands.DeleteSubmissionFile;
using TeachHub.Application.Features.Submissions.Commands.SubmitAssignment;
using TeachHub.Application.Features.Submissions.Commands.UploadSubmissionFile;
using TeachHub.Application.Features.Submissions.Queries.GetSubmission;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Class KHÔNG gắn policy: cùng tiền tố /api/assignments phục vụ cả giảng viên (tạo bài, cấu
// hình slot) lẫn sinh viên (tải tệp, nộp chính thức). [Authorize] ở class và ở action CỘNG DỒN
// chứ không ghi đè, nên đặt Instructor ở class rồi Student cho một action là KHÔNG AI qua được.
[Route("api/assignments")]
[Tags("Assignments")]
public sealed class AssignmentsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAssignmentCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetAssignmentQuery(id, CurrentUserId), ct));

    // Route tuyệt đối (bắt đầu bằng '/') nên bỏ qua [Route("api/assignments")] của class —
    // đường dẫn ra đúng /api/subject-versions/{id}/assignments theo hợp đồng SRS mục 7.
    [HttpGet("/api/subject-versions/{versionId:guid}/assignments")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<AssignmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListByVersion(Guid versionId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetAssignmentsQuery(versionId, CurrentUserId), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<AssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateAssignmentCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { AssignmentId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/slots")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubmissionSlotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSlot(Guid id, AddSubmissionSlotCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { AssignmentId = id, InstructorId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/slots/suggested")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<SubmissionSlotDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplySuggestedSlots(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new ApplySuggestedSlotsCommand { AssignmentId = id, InstructorId = CurrentUserId }, ct));

    // ── Phần của sinh viên ──────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/submissions/draft")]
    [Authorize(Policy = Policies.Student)]
    [Consumes("multipart/form-data")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(300_000_000)]                                 // hạn mức Kestrel
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]     // hạn mức multipart của MVC
    [ProducesResponseType<SubmissionFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadDraft(
        Guid id, [FromForm] UploadSubmissionForm form, CancellationToken ct)
    {
        // Controller mở stream; command chỉ nhận Stream. Đưa thẳng IFormFile xuống command
        // sẽ kéo Microsoft.AspNetCore.Http vào tầng Application.
        await using var noiDung = form.File.OpenReadStream();

        return ToActionResult(await sender.Send(new UploadSubmissionFileCommand(
            AssignmentId:  id,
            SlotKey:       form.SlotKey,
            FileName:      form.File.FileName,
            SizeBytes:     form.File.Length,
            Content:       noiDung,
            StudentUserId: CurrentUserId), ct));
    }

    [HttpPost("{id:guid}/submissions/submit")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<SubmissionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new SubmitAssignmentCommand(id, CurrentUserId), ct));

    [HttpGet("{id:guid}/my-submission")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<MyAssignmentSubmissionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MySubmission(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMySubmissionQuery(id, CurrentUserId), ct));

    // Route tuyệt đối: đường dẫn thuộc về tệp, không thuộc về bài tập.
    [HttpDelete("/api/submission-files/{fileId:guid}")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteFile(Guid fileId, CancellationToken ct)
        => ToActionResult(await sender.Send(new DeleteSubmissionFileCommand(fileId, CurrentUserId), ct));
}

/// <summary>
/// Bọc IFormFile trong lớp [FromForm] — bắt buộc, không phải thẩm mỹ. Với tham số rời,
/// OpenAPI sinh application/x-www-form-urlencoded và liệt kê thuộc tính nội bộ của IFormFile
/// thay vì một field nhị phân, nên client TypeScript sinh tự động không gọi được endpoint này.
/// </summary>
public sealed class UploadSubmissionForm
{
    [FromForm(Name = "file")]   // ghim tên trên dây; model binder form KHÔNG theo JsonNamingPolicy
    public required IFormFile File { get; init; }

    [FromForm(Name = "slot_key")]
    public required string SlotKey { get; init; }
}
