using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Features.Gradebook;

// ── Cấu hình cột điểm ───────────────────────────────────────────────────────────────────

public sealed record GradeColumnInput(
    Guid? Id, string Key, string Name, decimal WeightPercent, int OrderIndex,
    AggregationKind Kind, int N, bool TreatMissingAsZero);

public sealed record UpdateGradeColumnsCommand(List<GradeColumnInput> Columns) : ICommand<Result>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UpdateGradeColumnsValidator : AbstractValidator<UpdateGradeColumnsCommand>
{
    public UpdateGradeColumnsValidator()
    {
        RuleFor(x => x.Columns).NotEmpty().WithMessage("Sổ điểm phải có ít nhất một cột.");

        RuleForEach(x => x.Columns).ChildRules(c =>
        {
            c.RuleFor(y => y.Key).NotEmpty().MaximumLength(64).Matches("^[a-z0-9_]+$")
                .WithMessage("Khóa cột chỉ gồm chữ thường không dấu, số và gạch dưới (vd: thuong_ky).");
            c.RuleFor(y => y.Name).NotEmpty().MaximumLength(200);
            c.RuleFor(y => y.WeightPercent).InclusiveBetween(0m, 100m);
        });

        // Luật thuần hình dạng trên chính payload — cả bộ cột gửi trong một request nên nó tự
        // đủ dữ liệu ⇒ thuộc validator, chạy TRƯỚC khi TransactionBehavior mở transaction.
        RuleFor(x => x.Columns)
            .Must(c => c.Sum(y => y.WeightPercent) == 100m)
            .WithMessage(x => $"Tổng trọng số phải bằng 100%, hiện tại là {x.Columns.Sum(y => y.WeightPercent)}%.");

        RuleFor(x => x.Columns)
            .Must(c => c.Select(y => y.Key).Distinct().Count() == c.Count)
            .WithMessage("Có hai cột trùng khóa.");
    }
}

public sealed class UpdateGradeColumnsHandler(IAppDbContext db)
    : ICommandHandler<UpdateGradeColumnsCommand, Result>
{
    public async Task<Result> Handle(UpdateGradeColumnsCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy lớp học phần.");

        var hienCo = await db.GradeColumns
            .Where(c => c.OfferingId == request.OfferingId)
            .ToListAsync(ct);

        var giuLai = request.Columns.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToHashSet();
        var boDi = hienCo.Where(c => !giuLai.Contains(c.Id)).ToList();

        // Luật phụ thuộc dữ liệu đang có ⇒ ở handler, không ở validator: xóa một cột đã có
        // điểm là mất điểm thật. Trả 409 kèm con số để giảng viên biết mình đang định bỏ gì.
        if (boDi.Count > 0)
        {
            var idBoDi = boDi.Select(c => c.Id).ToList();
            var soO = await db.GradeRecords.CountAsync(
                r => idBoDi.Contains(r.GradeColumnId) && r.Score != null, ct);

            if (soO > 0)
                return Result.Conflict(
                    $"Cột định xóa đang có {soO} ô đã nhập điểm. Xóa cột là mất số điểm đó.");
        }

        db.GradeColumns.RemoveRange(boDi);

        foreach (var input in request.Columns)
        {
            var cot = input.Id is { } id ? hienCo.FirstOrDefault(c => c.Id == id) : null;

            if (cot is null)
            {
                db.GradeColumns.Add(new GradeColumn
                {
                    OfferingId = request.OfferingId,
                    Key = input.Key, Name = input.Name,
                    WeightPercent = input.WeightPercent, OrderIndex = input.OrderIndex,
                    Aggregation = new GradeAggregation
                    {
                        Kind = input.Kind, N = input.N, TreatMissingAsZero = input.TreatMissingAsZero,
                    },
                });
                continue;
            }

            cot.Key = input.Key;
            cot.Name = input.Name;
            cot.WeightPercent = input.WeightPercent;
            cot.OrderIndex = input.OrderIndex;
            cot.Aggregation.Kind = input.Kind;
            cot.Aggregation.N = input.N;
            cot.Aggregation.TreatMissingAsZero = input.TreatMissingAsZero;
        }

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ── Khởi tạo sổ điểm cho lớp chưa có cột nào ────────────────────────────────────────────

public sealed record InitGradebookCommand(Guid? TemplateId) : ICommand<Result>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class InitGradebookHandler(IAppDbContext db)
    : ICommandHandler<InitGradebookCommand, Result>
{
    public async Task<Result> Handle(InitGradebookCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy lớp học phần.");

        if (await db.GradeColumns.AnyAsync(c => c.OfferingId == request.OfferingId, ct))
            return Result.Conflict("Lớp này đã có cột điểm rồi.");

        // Mở lớp là SAO CHÉP mẫu thành cột rồi CẮT DÂY. Nếu cột chỉ là con trỏ tới mẫu thì
        // học kỳ sau sửa mẫu từ 20/30/50 sang 30/20/50 là bảng điểm đã nộp của kỳ trước lập
        // tức đổi số — không ai bấm nút nào, và không có gì trong hệ thống nói cho bạn biết.
        List<GradeColumn> cot;

        if (request.TemplateId is { } tid)
        {
            var mau = await db.GradeSchemeTemplates.AsNoTracking()
                .Include(t => t.Items)
                .SingleOrDefaultAsync(t => t.Id == tid && t.InstructorId == request.InstructorId, ct);

            if (mau is null) return Result.NotFound("Không tìm thấy bộ cột mẫu.");

            cot = [.. mau.Items.OrderBy(i => i.OrderIndex).Select(i => new GradeColumn
            {
                OfferingId = request.OfferingId,
                Key = i.Key, Name = i.Name,
                WeightPercent = i.WeightPercent, OrderIndex = i.OrderIndex,
                Aggregation = new GradeAggregation
                {
                    Kind = i.Aggregation.Kind, N = i.Aggregation.N,
                    TreatMissingAsZero = i.Aggregation.TreatMissingAsZero,
                },
            })];

            var lop = await db.CourseOfferings.SingleAsync(o => o.Id == request.OfferingId, ct);
            lop.GradeSchemeTemplateId = tid;   // chỉ ghi nhận nguồn gốc, không phép tính nào đọc
        }
        else
        {
            cot = GradeColumnFactory.MacDinh(request.OfferingId);
        }

        db.GradeColumns.AddRange(cot);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}

// ── Gắn nguồn điểm vào cột ──────────────────────────────────────────────────────────────

public sealed record SetColumnSourcesCommand(List<GradeColumnSourceInput> Sources) : ICommand<Result>
{
    public Guid ColumnId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed record GradeColumnSourceInput(GradeSourceType SourceType, Guid SourceId, decimal? WeightWithinColumn);

public sealed class SetColumnSourcesHandler(IAppDbContext db)
    : ICommandHandler<SetColumnSourcesCommand, Result>
{
    public async Task<Result> Handle(SetColumnSourcesCommand request, CancellationToken ct)
    {
        var cot = await db.GradeColumns.Include(c => c.Sources)
            .SingleOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (cot is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, cot.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy cột điểm.");

        // Add TƯỜNG MINH qua DbSet, không thêm vào navigation của parent đang tracked.
        //
        // Entity.Id tự sinh UUIDv7 NGAY LÚC KHỞI TẠO, nên khóa của một entity mới không bao
        // giờ mang giá trị mặc định. EF dùng chính điều đó để đoán trạng thái khi phát hiện
        // thay đổi qua navigation: thấy khóa khác default, nó kết luận hàng đã tồn tại và
        // phát UPDATE thay vì INSERT — rồi chết với "expected to affect 1 row(s), but
        // actually affected 0". db.Set.Add đặt state = Added dứt khoát, không đoán.
        db.GradeColumnSources.RemoveRange(cot.Sources);

        db.GradeColumnSources.AddRange(
            request.Sources.DistinctBy(x => (x.SourceType, x.SourceId)).Select(s => new GradeColumnSource
            {
                GradeColumnId = cot.Id,
                SourceType = s.SourceType,
                SourceId = s.SourceId,
                WeightWithinColumn = s.WeightWithinColumn,
            }));

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ── Sửa tay một ô điểm ──────────────────────────────────────────────────────────────────

public sealed record SetManualGradeCommand(Guid EnrollmentId, decimal? Score, string? Note)
    : ICommand<Result>
{
    public Guid ColumnId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class SetManualGradeValidator : AbstractValidator<SetManualGradeCommand>
{
    public SetManualGradeValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0m, 10m).When(x => x.Score is not null)
            .WithMessage("Điểm phải nằm trong khoảng 0 đến 10.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class SetManualGradeHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SetManualGradeCommand, Result>
{
    public async Task<Result> Handle(SetManualGradeCommand request, CancellationToken ct)
    {
        var cot = await db.GradeColumns.SingleOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (cot is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, cot.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy cột điểm.");

        // Cột phải thuộc đúng lớp của sinh viên đó — nếu không thì gõ một enrollment_id bất kỳ
        // là ghi điểm vào lớp người khác.
        if (!await db.Enrollments.AnyAsync(
                e => e.Id == request.EnrollmentId && e.OfferingId == cot.OfferingId, ct))
            return Result.NotFound("Sinh viên không thuộc lớp của cột điểm này.");

        var now = clock.GetUtcNow();

        var ghi = await db.GradeRecords.SingleOrDefaultAsync(
            r => r.EnrollmentId == request.EnrollmentId && r.GradeColumnId == request.ColumnId, ct);

        var truoc = ghi?.Score;

        if (ghi is null)
        {
            ghi = new GradeRecord
            {
                EnrollmentId = request.EnrollmentId,
                GradeColumnId = request.ColumnId,
            };
            db.GradeRecords.Add(ghi);
        }

        ghi.Score = request.Score;
        // Xóa điểm tay = trả ô về cho quy tắc tổng hợp tự tính lại.
        ghi.IsManualOverride = request.Score is not null;
        ghi.Note = request.Note;
        ghi.UpdatedAt = now;

        db.GradeAuditLogs.Add(new GradeAuditLog
        {
            OfferingId = cot.OfferingId,
            EnrollmentId = request.EnrollmentId,
            TargetType = GradeTargetType.GradeColumn,
            TargetId = request.ColumnId,
            OldValue = truoc,
            NewValue = request.Score,
            ChangedBy = request.InstructorId,
            ChangedAt = now,
            Reason = request.Note,
        });

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ── Cấu hình điểm cộng + điều kiện điểm sàn của lớp ─────────────────────────────────────

public sealed record UpdateGradebookSettingsCommand(
    BonusTarget BonusDefaultTarget, Guid? BonusDefaultColumnId,
    decimal? BonusMaxPointsPerStudent, List<GradeConditionDto> Conditions) : ICommand<Result>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UpdateGradebookSettingsValidator : AbstractValidator<UpdateGradebookSettingsCommand>
{
    public UpdateGradebookSettingsValidator()
    {
        RuleFor(x => x.BonusMaxPointsPerStudent).InclusiveBetween(0m, 10m)
            .When(x => x.BonusMaxPointsPerStudent is not null)
            .WithMessage("Trần điểm cộng phải nằm trong khoảng 0 đến 10.");

        RuleForEach(x => x.Conditions).ChildRules(c =>
        {
            c.RuleFor(y => y.ColumnKey).NotEmpty();
            c.RuleFor(y => y.Threshold).InclusiveBetween(0m, 10m);
            c.RuleFor(y => y.ActionValue).InclusiveBetween(0m, 10m);
        });
    }
}

public sealed class UpdateGradebookSettingsHandler(IAppDbContext db)
    : ICommandHandler<UpdateGradebookSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateGradebookSettingsCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy lớp học phần.");

        var lop = await db.CourseOfferings.SingleAsync(o => o.Id == request.OfferingId, ct);

        if (request.BonusDefaultTarget == BonusTarget.GradeColumn &&
            request.BonusDefaultColumnId is { } cid &&
            !await db.GradeColumns.AnyAsync(c => c.Id == cid && c.OfferingId == lop.Id, ct))
            return Result.NotFound("Cột mặc định không thuộc lớp này.");

        lop.BonusDefaultTarget = request.BonusDefaultTarget;
        lop.BonusDefaultColumnId = request.BonusDefaultTarget == BonusTarget.GradeColumn
            ? request.BonusDefaultColumnId
            : null;
        lop.BonusMaxPointsPerStudent = request.BonusMaxPointsPerStudent;

        lop.GradeConditions = [.. request.Conditions.Select(d => new GradeCondition
        {
            ColumnKey = d.ColumnKey, Op = d.Op, Threshold = d.Threshold,
            Action = d.Action, ActionValue = d.ActionValue,
        })];

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
