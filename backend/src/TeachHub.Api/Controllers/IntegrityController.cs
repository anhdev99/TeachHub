using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Integrity;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

/// <summary>
/// Liêm chính học thuật (Phase 6a) và chấm nâng cao (Phase 6b).
///
/// Class KHÔNG mang policy: cùng một controller phục vụ cả giảng viên (báo cáo trùng lặp,
/// điểm riêng thành viên) lẫn sinh viên (mở gợi ý). <c>[Authorize]</c> ở class và ở action
/// CỘNG DỒN chứ không ghi đè — đặt policy Instructor ở class rồi Student ở một action là đòi
/// tài khoản có CẢ HAI vai, và không ai vào được action đó.
///
/// ⚠️ Không có endpoint nào ở đây tự trừ điểm vì phát hiện trùng lặp. Hệ thống CẢNH BÁO,
/// con người quyết định (Rủi ro #3 của SRS mục 11). Chấm gian lận tự động một lần sai là một
/// cáo buộc sai nhắm vào một sinh viên thật.
/// </summary>
[Route("api")]
[Tags("Integrity")]
public sealed class IntegrityController(ISender sender) : ApiControllerBase
{
    // ── 6a — Phát hiện trùng lặp mã nguồn ───────────────────────────────────────────────

    /// <summary>
    /// Báo cáo trùng lặp của một bài tập trong một lớp.
    /// <paramref name="threshold"/> để trống thì dùng ngưỡng mặc định — ngưỡng PHẢI hiệu chỉnh
    /// theo từng loại bài: bài nhập môn dùng chung starter code trùng 70–90% chính đáng.
    /// </summary>
    [HttpGet("assignments/{assignmentId:guid}/similarity")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SimilarityReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SimilarityReport(
        Guid assignmentId,
        // Model binder của query string KHÔNG theo JsonNamingPolicy — tham số nhiều chữ phải
        // khai [FromQuery(Name = ...)] tường minh, nếu không nó lặng lẽ luôn nhận null.
        [FromQuery(Name = "offering_id")] Guid offeringId,
        [FromQuery(Name = "threshold")] decimal? threshold,
        CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetSimilarityReportQuery(assignmentId, offeringId, CurrentUserId, threshold), ct));

    /// <summary>Dữ liệu tô sáng cho màn hình so sánh cạnh nhau hai bài nộp.</summary>
    [HttpGet("submissions/{id:guid}/compare/{otherId:guid}")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<SubmissionComparisonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compare(Guid id, Guid otherId, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new CompareSubmissionsQuery(id, otherId, CurrentUserId), ct));

    // ── 6b — Gợi ý mở dần ───────────────────────────────────────────────────────────────

    [HttpGet("assignments/{assignmentId:guid}/hints")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<List<HintDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MyHints(Guid assignmentId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMyHintsQuery(assignmentId, CurrentUserId), ct));

    /// <summary>Mở một gợi ý. Mở lại gợi ý đã mở KHÔNG trừ điểm lần nữa.</summary>
    [HttpPost("assignment-hints/{id:guid}/unlock")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<HintDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockHint(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new UnlockHintCommand(id, CurrentUserId), ct));

    [HttpGet("assignments/{assignmentId:guid}/hint-unlocks")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<HintUnlockLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HintUnlockLog(
        Guid assignmentId,
        [FromQuery(Name = "offering_id")] Guid offeringId,
        CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetHintUnlockLogQuery(assignmentId, offeringId, CurrentUserId), ct));

    // ── 6b — Phạt trễ và phạt gợi ý ─────────────────────────────────────────────────────

    /// <summary>
    /// Tính lại điểm hiệu lực từ điểm gốc. Gọi lại bao nhiêu lần cũng cho cùng kết quả.
    /// </summary>
    [HttpPost("submissions/{id:guid}/apply-penalties")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<ScoreBreakdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyPenalties(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new ApplySubmissionPenaltiesCommand { SubmissionId = id, InstructorId = CurrentUserId }, ct));

    // ── 6b — Điểm riêng từng thành viên nhóm ────────────────────────────────────────────

    [HttpGet("submissions/{id:guid}/member-scores")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<MemberEffectiveScoreDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MemberScores(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetMemberScoresQuery(id, CurrentUserId), ct));

    [HttpPut("submissions/{id:guid}/member-scores")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<List<MemberEffectiveScoreDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetMemberScores(
        Guid id, SetMemberScoresCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { SubmissionId = id, InstructorId = CurrentUserId }, ct));
}
