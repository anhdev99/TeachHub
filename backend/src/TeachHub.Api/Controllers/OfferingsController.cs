using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Offerings;
using TeachHub.Application.Features.Offerings.Commands.CreateOffering;
using TeachHub.Application.Features.Offerings.Queries.GetOffering;
using TeachHub.Application.Features.Offerings.Queries.GetOfferings;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Class KHÔNG gắn policy: controller này phục vụ cả giảng viên lẫn sinh viên (action Join,
// MyOfferings ở Task 2.3). [Authorize] ở class và ở action CỘNG DỒN chứ không ghi đè, nên
// đặt Policies.Instructor ở class rồi Policies.Student cho một action là KHÔNG AI qua được cả hai.
[Route("api/offerings")]
[Tags("Offerings")]
public sealed class OfferingsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<OfferingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOfferingCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<IReadOnlyList<OfferingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? subjectId, CancellationToken ct)
        => Ok(await sender.Send(new GetOfferingsQuery(CurrentUserId, subjectId), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<OfferingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetOfferingQuery(id, CurrentUserId), ct));
}
