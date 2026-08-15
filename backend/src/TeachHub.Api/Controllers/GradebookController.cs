using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Infrastructure;

namespace TeachHub.Api.Controllers;

// Class để [Authorize] trần — SetFallbackPolicy đã bắt buộc đăng nhập — rồi gắn
// Policies.Instructor xuống TỪNG action. Sổ điểm là dữ liệu nhạy cảm nhất hệ thống, và vai
// trợ giảng hiện chưa có cách giới hạn theo lớp (ADR-013) nên không được cấp quyền đọc ở đây.
[Route("api")]
[Tags("Gradebook")]
public sealed class GradebookController(ISender sender) : ApiControllerBase
{
    [HttpGet("offerings/{offeringId:guid}/gradebook")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType<GradebookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid offeringId,
        // Model binder của query string KHÔNG theo JsonNamingPolicy: ?provisional=true sẽ
        // không bind vào một tham số tên khác, và bạn không nhận được lỗi nào — cờ luôn false.
        [FromQuery(Name = "provisional")] bool provisional,
        CancellationToken ct)
        => ToActionResult(await sender.Send(
            new GetGradebookQuery(offeringId, CurrentUserId, provisional), ct));

    [HttpPost("offerings/{offeringId:guid}/gradebook/init")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Init(
        Guid offeringId, InitGradebookCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpPut("offerings/{offeringId:guid}/grade-columns")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateColumns(
        Guid offeringId, UpdateGradeColumnsCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpPut("grade-columns/{columnId:guid}/sources")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetSources(
        Guid columnId, SetColumnSourcesCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { ColumnId = columnId, InstructorId = CurrentUserId }, ct));

    [HttpPut("grade-columns/{columnId:guid}/grades")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetManualGrade(
        Guid columnId, SetManualGradeCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { ColumnId = columnId, InstructorId = CurrentUserId }, ct));

    [HttpPut("offerings/{offeringId:guid}/gradebook/settings")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings(
        Guid offeringId, UpdateGradebookSettingsCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(
            command with { OfferingId = offeringId, InstructorId = CurrentUserId }, ct));

    [HttpGet("offerings/{offeringId:guid}/gradebook/export")]
    [Authorize(Policy = Policies.Instructor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        Guid offeringId,
        [FromQuery(Name = "template_id")] Guid? templateId,
        CancellationToken ct)
    {
        var kq = await sender.Send(new ExportGradebookQuery(offeringId, CurrentUserId, templateId), ct);
        if (!kq.IsSuccess) return ToActionResult(kq);

        return File(kq.Value.Content, kq.Value.ContentType, kq.Value.FileName);
    }
}

[Route("api")]
[Tags("Gradebook")]
[Authorize(Policy = Policies.Instructor)]
public sealed class BonusPointsController(ISender sender) : ApiControllerBase
{
    [HttpGet("offerings/{offeringId:guid}/bonus-points")]
    [ProducesResponseType<List<BonusPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOffering(Guid offeringId, CancellationToken ct)
        => ToActionResult(await sender.Send(new GetBonusPointsQuery(offeringId, CurrentUserId), ct));

    [HttpPost("bonus-points")]
    [ProducesResponseType<BonusPointDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grant(GrantBonusPointCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpDelete("bonus-points/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await sender.Send(new DeleteBonusPointCommand(id, CurrentUserId), ct));

    [HttpGet("instructor/bonus-reasons")]
    [ProducesResponseType<List<BonusReasonTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReasons(
        [FromQuery(Name = "include_hidden")] bool includeHidden, CancellationToken ct)
        => Ok(await sender.Send(new GetBonusReasonTemplatesQuery(CurrentUserId, includeHidden), ct));

    [HttpPost("instructor/bonus-reasons")]
    [ProducesResponseType<BonusReasonTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveReason(
        SaveBonusReasonTemplateCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpPut("instructor/bonus-reasons/{id:guid}/visibility")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetReasonVisibility(
        Guid id, [FromQuery(Name = "is_active")] bool isActive, CancellationToken ct)
        => ToActionResult(await sender.Send(
            new SetBonusReasonTemplateVisibilityCommand(id, isActive, CurrentUserId), ct));
}

[Route("api/instructor")]
[Tags("Gradebook")]
[Authorize(Policy = Policies.Instructor)]
public sealed class GradeTemplatesController(ISender sender) : ApiControllerBase
{
    [HttpGet("grade-scheme-templates")]
    [ProducesResponseType<List<GradeSchemeTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchemes(CancellationToken ct)
        => Ok(await sender.Send(new GetSchemeTemplatesQuery(CurrentUserId), ct));

    [HttpPost("grade-scheme-templates")]
    [ProducesResponseType<GradeSchemeTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveScheme(
        SaveGradeSchemeTemplateCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));

    [HttpGet("grade-export-templates")]
    [ProducesResponseType<List<GradeExportTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExports(CancellationToken ct)
        => Ok(await sender.Send(new GetExportTemplatesQuery(CurrentUserId), ct));

    [HttpPost("grade-export-templates")]
    [ProducesResponseType<GradeExportTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveExport(
        SaveGradeExportTemplateCommand command, CancellationToken ct)
        => ToActionResult(await sender.Send(command with { InstructorId = CurrentUserId }, ct));
}
