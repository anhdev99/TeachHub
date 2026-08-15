using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Features.Gradebook;

// ── Cấp điểm cộng ───────────────────────────────────────────────────────────────────────

public sealed record GrantBonusPointCommand(
    Guid EnrollmentId, BonusTarget Target, Guid? TargetColumnId,
    decimal Points, string Reason, Guid? TemplateId) : ICommand<Result<BonusPointDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class GrantBonusPointValidator : AbstractValidator<GrantBonusPointCommand>
{
    public GrantBonusPointValidator()
    {
        // Điểm cộng luôn > 0. Muốn trừ điểm thì sửa điểm cột — cho phép số âm ở đây nghĩa là
        // có hai đường trừ điểm và không ai biết đường nào đang có hiệu lực.
        RuleFor(x => x.Points).GreaterThan(0m).WithMessage("Điểm cộng phải lớn hơn 0.")
            .LessThanOrEqualTo(10m).WithMessage("Điểm cộng một lần không vượt quá 10.");

        RuleFor(x => x.Reason).NotEmpty().WithMessage("Phải ghi lý do cấp điểm cộng.")
            .MaximumLength(500);

        RuleFor(x => x.TargetColumnId).NotNull()
            .When(x => x.Target == BonusTarget.GradeColumn)
            .WithMessage("Cộng vào cột thì phải chọn cột nào.");

        RuleFor(x => x.TargetColumnId).Null()
            .When(x => x.Target == BonusTarget.FinalTotal)
            .WithMessage("Cộng thẳng điểm tổng thì không được chọn cột.");
    }
}

public sealed class GrantBonusPointHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<GrantBonusPointCommand, Result<BonusPointDto>>
{
    public async Task<Result<BonusPointDto>> Handle(
        GrantBonusPointCommand request, CancellationToken ct)
    {
        var sv = await db.Enrollments.AsNoTracking()
            .Where(e => e.Id == request.EnrollmentId)
            .Select(e => new { e.Id, e.OfferingId, e.FullName })
            .SingleOrDefaultAsync(ct);

        if (sv is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, sv.OfferingId, request.InstructorId, ct))
            return Result<BonusPointDto>.NotFound("Không tìm thấy sinh viên.");

        if (request.TargetColumnId is { } cid &&
            !await db.GradeColumns.AnyAsync(c => c.Id == cid && c.OfferingId == sv.OfferingId, ct))
            return Result<BonusPointDto>.NotFound("Cột điểm không thuộc lớp của sinh viên này.");

        var now = clock.GetUtcNow();

        var bp = new BonusPoint
        {
            EnrollmentId = sv.Id,
            OfferingId = sv.OfferingId,
            Target = request.Target,
            TargetColumnId = request.TargetColumnId,
            Points = request.Points,

            // Reason là BẢN SAO văn bản tại thời điểm cấp, không chỉ là khóa ngoại tới
            // template: sáu tháng sau sửa nhãn hoặc ẩn template thì mọi điểm cộng đã cấp từ
            // đầu kỳ sẽ đổi nghĩa hoặc mất chữ. TemplateId giữ lại chỉ để thống kê.
            Reason = request.Reason.Trim(),
            TemplateId = request.TemplateId,
            GrantedBy = request.InstructorId,
            GrantedAt = now,
        };

        db.BonusPoints.Add(bp);

        db.GradeAuditLogs.Add(new GradeAuditLog
        {
            OfferingId = sv.OfferingId,
            EnrollmentId = sv.Id,
            TargetType = GradeTargetType.BonusPoint,
            TargetId = bp.Id,
            OldValue = null,
            NewValue = request.Points,
            ChangedBy = request.InstructorId,
            ChangedAt = now,
            Reason = bp.Reason,
        });

        await db.SaveChangesAsync(ct);

        return Result<BonusPointDto>.Ok(new BonusPointDto(
            bp.Id, bp.EnrollmentId, sv.FullName, bp.Target, bp.TargetColumnId,
            bp.Points, bp.Reason, bp.GrantedAt));
    }
}

// ── Xóa điểm cộng ───────────────────────────────────────────────────────────────────────

public sealed record DeleteBonusPointCommand(Guid Id, Guid InstructorId) : ICommand<Result>;

public sealed class DeleteBonusPointHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<DeleteBonusPointCommand, Result>
{
    public async Task<Result> Handle(DeleteBonusPointCommand request, CancellationToken ct)
    {
        var bp = await db.BonusPoints.SingleOrDefaultAsync(b => b.Id == request.Id, ct);

        if (bp is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, bp.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy điểm cộng.");

        // Xóa điểm cộng vẫn để lại vết: nhật ký chỉ ghi thêm, không bao giờ bị dọn theo.
        db.GradeAuditLogs.Add(new GradeAuditLog
        {
            OfferingId = bp.OfferingId,
            EnrollmentId = bp.EnrollmentId,
            TargetType = GradeTargetType.BonusPoint,
            TargetId = bp.Id,
            OldValue = bp.Points,
            NewValue = null,
            ChangedBy = request.InstructorId,
            ChangedAt = clock.GetUtcNow(),
            Reason = $"Gỡ điểm cộng: {bp.Reason}",
        });

        db.BonusPoints.Remove(bp);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}

// ── Đọc điểm cộng của một lớp ───────────────────────────────────────────────────────────

public sealed record GetBonusPointsQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<List<BonusPointDto>>>;

public sealed class GetBonusPointsHandler(IAppDbContext db)
    : IQueryHandler<GetBonusPointsQuery, Result<List<BonusPointDto>>>
{
    public async Task<Result<List<BonusPointDto>>> Handle(
        GetBonusPointsQuery request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<List<BonusPointDto>>.NotFound("Không tìm thấy lớp học phần.");

        var ds = await db.BonusPoints.AsNoTracking()
            .Where(b => b.OfferingId == request.OfferingId)
            .OrderByDescending(b => b.GrantedAt)
            .Select(b => new BonusPointDto(
                b.Id, b.EnrollmentId,
                db.Enrollments.Where(e => e.Id == b.EnrollmentId).Select(e => e.FullName).Single(),
                b.Target, b.TargetColumnId, b.Points, b.Reason, b.GrantedAt))
            .ToListAsync(ct);

        return Result<List<BonusPointDto>>.Ok(ds);
    }
}

// ── Thư viện lý do ──────────────────────────────────────────────────────────────────────

public sealed record GetBonusReasonTemplatesQuery(Guid InstructorId, bool IncludeHidden)
    : IQuery<List<BonusReasonTemplateDto>>;

public sealed class GetBonusReasonTemplatesHandler(IAppDbContext db)
    : IQueryHandler<GetBonusReasonTemplatesQuery, List<BonusReasonTemplateDto>>
{
    public async Task<List<BonusReasonTemplateDto>> Handle(
        GetBonusReasonTemplatesQuery request, CancellationToken ct) =>
        await db.BonusPointReasonTemplates.AsNoTracking()
            .Where(t => t.InstructorId == request.InstructorId)
            .Where(t => request.IncludeHidden || t.IsActive)
            .OrderBy(t => t.OrderIndex)
            .Select(t => new BonusReasonTemplateDto(
                t.Id, t.Label, t.DefaultPoints, t.DefaultTarget,
                t.DefaultColumnKey, t.OrderIndex, t.IsActive))
            .ToListAsync(ct);
}

public sealed record SaveBonusReasonTemplateCommand(
    Guid? Id, string Label, decimal DefaultPoints, BonusTarget? DefaultTarget,
    string? DefaultColumnKey) : ICommand<Result<BonusReasonTemplateDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveBonusReasonTemplateValidator : AbstractValidator<SaveBonusReasonTemplateCommand>
{
    public SaveBonusReasonTemplateValidator()
    {
        RuleFor(x => x.Label).NotEmpty().WithMessage("Chưa nhập nhãn lý do.").MaximumLength(200);
        RuleFor(x => x.DefaultPoints).GreaterThan(0m).LessThanOrEqualTo(10m)
            .WithMessage("Điểm gợi ý phải nằm trong khoảng lớn hơn 0 và tối đa 10.");
    }
}

public sealed class SaveBonusReasonTemplateHandler(IAppDbContext db)
    : ICommandHandler<SaveBonusReasonTemplateCommand, Result<BonusReasonTemplateDto>>
{
    public async Task<Result<BonusReasonTemplateDto>> Handle(
        SaveBonusReasonTemplateCommand request, CancellationToken ct)
    {
        BonusPointReasonTemplate mau;

        if (request.Id is { } id)
        {
            var tim = await db.BonusPointReasonTemplates
                .SingleOrDefaultAsync(t => t.Id == id && t.InstructorId == request.InstructorId, ct);

            if (tim is null) return Result<BonusReasonTemplateDto>.NotFound("Không tìm thấy lý do.");
            mau = tim;
        }
        else
        {
            var thuTu = await db.BonusPointReasonTemplates
                .Where(t => t.InstructorId == request.InstructorId)
                .Select(t => (int?)t.OrderIndex).MaxAsync(ct) ?? -1;

            mau = new BonusPointReasonTemplate
            {
                InstructorId = request.InstructorId,
                Label = request.Label,
                DefaultPoints = request.DefaultPoints,
                OrderIndex = thuTu + 1,
            };

            db.BonusPointReasonTemplates.Add(mau);
        }

        mau.Label = request.Label.Trim();
        mau.DefaultPoints = request.DefaultPoints;
        mau.DefaultTarget = request.DefaultTarget;
        mau.DefaultColumnKey = request.DefaultColumnKey?.Trim();

        await db.SaveChangesAsync(ct);

        return Result<BonusReasonTemplateDto>.Ok(new BonusReasonTemplateDto(
            mau.Id, mau.Label, mau.DefaultPoints, mau.DefaultTarget,
            mau.DefaultColumnKey, mau.OrderIndex, mau.IsActive));
    }
}

public sealed record SetBonusReasonTemplateVisibilityCommand(Guid Id, bool IsActive, Guid InstructorId)
    : ICommand<Result>;

public sealed class SetBonusReasonTemplateVisibilityHandler(IAppDbContext db)
    : ICommandHandler<SetBonusReasonTemplateVisibilityCommand, Result>
{
    public async Task<Result> Handle(
        SetBonusReasonTemplateVisibilityCommand request, CancellationToken ct)
    {
        var mau = await db.BonusPointReasonTemplates
            .SingleOrDefaultAsync(t => t.Id == request.Id && t.InstructorId == request.InstructorId, ct);

        if (mau is null) return Result.NotFound("Không tìm thấy lý do.");

        // ẨN chứ không xóa: điểm cộng đã cấp giữ bản sao văn bản nên không mất chữ, nhưng
        // xóa hẳn template thì thống kê "lý do nào hay dùng" mất một mảng lịch sử.
        mau.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
