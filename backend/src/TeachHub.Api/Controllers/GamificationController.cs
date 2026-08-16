using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Gamification;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

/// <summary>
/// Thư viện huy hiệu — thuộc GIẢNG VIÊN, dùng lại cho mọi lớp và mọi học kỳ, cùng khuôn với
/// ngân hàng câu hỏi.
/// </summary>
[Route("api/badges")]
[Tags("Gamification")]
[Authorize(Policy = Policies.Instructor)]
public sealed class BadgesController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<BadgeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetBadgesQuery(CurrentUserId), ct));

    [HttpPost]
    [ProducesResponseType<BadgeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(SaveBadgeCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new DeleteBadgeCommand(id, CurrentUserId), ct));
}

// Class KHÔNG mang policy: cùng tiền tố phục vụ cả giảng viên (cấu hình, tính lại) lẫn sinh
// viên (trang tiến độ, bảng xếp hạng). [Authorize] ở class và ở action CỘNG DỒN chứ không ghi
// đè — đặt Instructor ở class rồi thêm Student ở một action là không tài khoản nào vào được,
// và triệu chứng là 403 câm chứ không phải một lỗi cấu hình dễ thấy.
[Route("api")]
[Tags("Gamification")]
public sealed class GamificationController(ISender sender) : ApiControllerBase
{
    // ── Bảng quy tắc XP (giảng viên) ────────────────────────────────────────────────────

    [HttpGet("offerings/{offeringId:guid}/xp-rules")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<XpRuleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetXpRules(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetXpRulesQuery(offeringId, CurrentUserId), ct));

    [HttpPost("offerings/{offeringId:guid}/xp-rules")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<XpRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveXpRule(
        Guid offeringId, SaveXpRuleCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpPost("offerings/{offeringId:guid}/xp-rules/seed")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<XpRuleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SeedXpRules(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new SeedXpRulesCommand(offeringId) { InstructorId = CurrentUserId }, ct));

    [HttpDelete("xp-rules/{id:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteXpRule(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new DeleteXpRuleCommand(id, CurrentUserId), ct));

    // ── Cấu hình động lực học tập của lớp (giảng viên) ───────────────────────────────────

    [HttpGet("offerings/{offeringId:guid}/gamification-settings")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<GamificationSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetGamificationSettingsQuery(offeringId, CurrentUserId), ct));

    [HttpPut("offerings/{offeringId:guid}/gamification-settings")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<GamificationSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveSettings(
        Guid offeringId, SaveGamificationSettingsCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpPost("offerings/{offeringId:guid}/gamification/recalculate")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<RecalculationSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recalculate(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new RecalculateOfferingGamificationCommand(offeringId) { InstructorId = CurrentUserId }, ct));

    /// <summary>Bảng xếp hạng phía giảng viên — luôn hiện tên thật, đây là lớp của họ.</summary>
    [HttpGet("offerings/{offeringId:guid}/gamification/leaderboard")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<LeaderboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InstructorLeaderboard(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetOfferingLeaderboardQuery(offeringId, CurrentUserId), ct));

    // ── Phía sinh viên ──────────────────────────────────────────────────────────────────

    [HttpGet("offerings/{offeringId:guid}/my-gamification")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<MyGamificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MyGamification(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMyGamificationQuery(offeringId, CurrentUserId), ct));

    /// <summary>
    /// Tính lại XP và huy hiệu của chính mình từ hoạt động đã có. KHÔNG nhận số XP từ client:
    /// một endpoint nhận điểm do client gửi là endpoint mà sinh viên đầu tiên biết mở DevTools
    /// sẽ dùng để đứng đầu bảng xếp hạng.
    /// </summary>
    [HttpPost("offerings/{offeringId:guid}/my-gamification/sync")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<MyGamificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncMyGamification(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new SyncMyGamificationCommand(offeringId) { UserId = CurrentUserId }, ct));

    [HttpGet("offerings/{offeringId:guid}/leaderboard")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<LeaderboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leaderboard(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetLeaderboardQuery(offeringId, CurrentUserId), ct));
}
