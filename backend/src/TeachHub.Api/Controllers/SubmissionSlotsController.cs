using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Assignments;
using TeachHub.Application.Features.Assignments.Commands.ManageSlots;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/submission-slots")]
[Tags("Assignments")]
public sealed class SubmissionSlotsController(ISender sender) : ApiControllerBase
{
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubmissionSlotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateSubmissionSlotCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SlotId = id, InstructorId = CurrentUserId }, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new DeleteSubmissionSlotCommand { SlotId = id, InstructorId = CurrentUserId }, ct));

    /// <summary>
    /// Đổi thứ tự slot. Cùng khuôn với <c>PUT /api/lessons/{id}/reorder</c> của Phase 1 —
    /// thiếu endpoint này thì giao diện kéo-thả được nhưng thả xong không lưu được, và giảng
    /// viên sắp xong, tải lại trang, thứ tự về như cũ mà không có thông báo lỗi nào.
    /// </summary>
    [HttpPut("{id:guid}/reorder")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        Guid id, ReorderSubmissionSlotCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SlotId = id, InstructorId = CurrentUserId }, ct));
}
