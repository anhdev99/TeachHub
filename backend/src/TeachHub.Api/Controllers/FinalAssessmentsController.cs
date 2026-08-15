using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Assessments;
using TeachHub.Application.Features.Assignments;
using TeachHub.Application.Features.Dashboard;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api")]
[Tags("FinalAssessments")]
public sealed class FinalAssessmentsController(ISender sender) : ApiControllerBase
{
    // Class không mang policy: action đọc danh sách dùng chung cho cả sinh viên (xem đợt
    // cuối môn của lớp mình) lẫn giảng viên. Fallback policy đã bắt buộc đăng nhập.
    [HttpGet("offerings/{offeringId:guid}/final-assessments")]
    [ProducesResponseType<List<FinalAssessmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOffering(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetFinalAssessmentsQuery(offeringId, CurrentUserId), ct));

    [HttpPost("offerings/{offeringId:guid}/final-assessments")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<FinalAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        Guid offeringId, CreateFinalAssessmentCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpPost("final-assessments/{id:guid}/slots")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubmissionSlotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSlot(
        Guid id, AddFinalAssessmentSlotCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { FinalAssessmentId = id, InstructorId = CurrentUserId }, ct));
}

[Route("api")]
[Tags("Dashboard")]
public sealed class DashboardController(ISender sender) : ApiControllerBase
{
    // Đường dẫn của SRS mục 7 là /api/instructor/dashboard — "instructor" đứng TRƯỚC
    // "dashboard". Đảo thành [Route("api/dashboard")] + [HttpGet("instructor")] ra
    // /api/dashboard/instructor: build xanh, test tự viết cũng xanh, chỉ có client TS sinh
    // từ OpenAPI là trỏ vào một đường dẫn trả 404 — và bạn phát hiện khi trang chủ trắng trơn.
    [HttpGet("instructor/dashboard")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<InstructorDashboardDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstructorDashboard(CancellationToken ct)
        => Ok(await sender.Send(new GetInstructorDashboardQuery(CurrentUserId), ct));
}
