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
}
