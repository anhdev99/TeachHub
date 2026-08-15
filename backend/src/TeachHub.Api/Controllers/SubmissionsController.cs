using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Grading;
using TeachHub.Application.Features.Submissions;
using TeachHub.Application.Features.Submissions.Queries.GetMyAssignments;
using TeachHub.Application.Features.Submissions.Queries.GetSubmission;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// KHÔNG gắn [Authorize] theo vai trò ở đây: cả sinh viên (bài của chính mình) lẫn giảng viên
// (bài của lớp mình) đều dùng chung các endpoint này. Fallback policy đã bắt buộc đăng nhập;
// quyền xem từng bài do handler quyết định và trả 404 chứ không 403.
[Route("api/submissions")]
[Tags("Submissions")]
public sealed class SubmissionsController(ISender sender) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SubmissionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetSubmissionQuery(id, CurrentUserId), ct));

    [HttpGet("{id:guid}/files/{fileId:guid}/file-tree")]
    [ProducesResponseType<FileTreeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FileTree(Guid id, Guid fileId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetFileTreeQuery(id, fileId, CurrentUserId), ct));

    [HttpGet("{id:guid}/files/{fileId:guid}/entry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Entry(
        Guid id, Guid fileId, [FromQuery] string path, CancellationToken ct)
    {
        var kq = await sender.Send(new GetZipEntryQuery(id, fileId, path, CurrentUserId), ct);
        if (!kq.IsSuccess) return ToActionResult(kq);

        // LUÔN text/plain + Content-Disposition: attachment. Một file .html do sinh viên nộp
        // mà render được trong tab của giảng viên là chạy script trong phiên đăng nhập của
        // người có quyền cao nhất hệ thống. File(...) tự đặt attachment khi có fileDownloadName.
        return File(kq.Value.Content, "text/plain; charset=utf-8", fileDownloadName: kq.Value.FileName);
    }

    // Route tuyệt đối (bắt đầu bằng '/') nên bỏ qua [Route("api/submissions")] của class.
    [HttpGet("/api/offerings/{offeringId:guid}/assignments")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<List<MyAssignmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MyAssignments(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMyAssignmentsQuery(offeringId, CurrentUserId), ct));

    // ── Phase 4a: chấm bài ──────────────────────────────────────────────────────────────
    // Thêm vào chính lớp này chứ KHÔNG tạo file controller thứ hai cùng route: hai lớp cùng
    // tên là CS0101 lúc biên dịch, còn hai action cùng đường dẫn là AmbiguousMatchException
    // lúc chạy — lỗi thứ hai không có gì ở tầng biên dịch bắt được.

    [HttpPost("{id:guid}/grade")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<GradedSubmissionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grade(Guid id, GradeSubmissionCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SubmissionId = id, GraderId = CurrentUserId }, ct));

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Return(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new ReturnSubmissionCommand(id, CurrentUserId), ct));

    [HttpGet("/api/offerings/{offeringId:guid}/submissions")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<ToGradeItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToGrade(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetSubmissionsToGradeQuery(offeringId, CurrentUserId), ct));
}
