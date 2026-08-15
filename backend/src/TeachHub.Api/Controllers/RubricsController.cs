using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Rubrics;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

[Route("api/rubrics")]
[Tags("Rubrics")]
public sealed class RubricsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<RubricDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetRubricsQuery(CurrentUserId), ct));

    [HttpPost]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<RubricDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateRubricCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));
}
