using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Groups;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Route ở cấp class chỉ là "api" vì tài nguyên nhóm sống dưới HAI tiền tố:
// /api/offerings/{id}/groups khi thao tác theo lớp, /api/groups/{id}/… khi đã biết id nhóm.
// Class KHÔNG mang policy: action Join dành cho sinh viên, mà policy ở class thì cộng dồn
// chứ không ghi đè — đặt Instructor ở đây là không sinh viên nào tham gia nhóm được.
[Route("api")]
[Tags("Groups")]
public sealed class GroupsController(ISender sender) : ApiControllerBase
{
    [HttpGet("offerings/{offeringId:guid}/groups")]
    [ProducesResponseType<List<GroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOffering(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetGroupsQuery(offeringId, CurrentUserId), ct));

    [HttpPost("offerings/{offeringId:guid}/groups")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid offeringId, CreateGroupCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, ActorId = CurrentUserId }, ct));

    [HttpPost("offerings/{offeringId:guid}/groups/auto-generate")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<GroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AutoGenerate(
        Guid offeringId, AutoGenerateGroupsCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, ActorId = CurrentUserId }, ct));

    [HttpPut("groups/{id:guid}/members")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMembers(
        Guid id, UpdateGroupMembersCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { GroupId = id, ActorId = CurrentUserId }, ct));

    /// <summary>Sinh viên tự tham gia, nếu lớp cho phép.</summary>
    [HttpPost("groups/{id:guid}/join")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new JoinGroupCommand(id, CurrentUserId), ct));
}
