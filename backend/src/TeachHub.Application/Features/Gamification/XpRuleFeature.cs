using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

// ── Đọc bảng quy tắc XP của một lớp ─────────────────────────────────────────────────────

public sealed record GetXpRulesQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<List<XpRuleDto>>>;

public sealed class GetXpRulesHandler(IAppDbContext db)
    : IQueryHandler<GetXpRulesQuery, Result<List<XpRuleDto>>>
{
    public async Task<Result<List<XpRuleDto>>> Handle(GetXpRulesQuery request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<List<XpRuleDto>>.NotFound("Không tìm thấy lớp học phần.");

        var ds = await db.XpRules.AsNoTracking()
            .Where(r => r.OfferingId == request.OfferingId)
            .OrderBy(r => r.EventType).ThenBy(r => r.MinValue)
            .ToListAsync(ct);

        return Result<List<XpRuleDto>>.Ok([.. ds.Select(XpRuleDto.From)]);
    }
}

// ── Tạo / sửa một quy tắc ───────────────────────────────────────────────────────────────

public sealed record SaveXpRuleCommand(
    Guid? Id, XpEventType EventType, int Points, decimal? MinValue, int? DailyCap, bool IsEnabled)
    : ICommand<Result<XpRuleDto>>
{
    // Trường server tự điền là property init-only, KHÔNG phải tham số positional có `= default`:
    // JsonSchemaExporter ném JsonException với kiểu giá trị mang giá trị mặc định, và khi đó
    // TOÀN BỘ /openapi/v1.json trả 500 chứ không riêng endpoint này.
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class SaveXpRuleValidator : AbstractValidator<SaveXpRuleCommand>
{
    public SaveXpRuleValidator()
    {
        // Luật thuần hình dạng trên payload ⇒ validator. Quyền sở hữu lớp phải đọc DB ⇒ handler.
        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("Số XP phải lớn hơn 0.")
            .LessThanOrEqualTo(1000).WithMessage("Số XP mỗi lần không được vượt quá 1000.");

        RuleFor(x => x.MinValue)
            .GreaterThanOrEqualTo(0m).When(x => x.MinValue is not null)
            .WithMessage("Ngưỡng không được âm.");

        RuleFor(x => x.DailyCap)
            .GreaterThan(0).When(x => x.DailyCap is not null)
            .WithMessage("Trần mỗi ngày phải lớn hơn 0.");

        // Mốc streak mà không có ngưỡng thì không biết mốc mấy ngày — quy tắc vô nghĩa và
        // sẽ không bao giờ kích hoạt, mà cũng không báo lỗi gì cho tới khi có người thắc mắc.
        RuleFor(x => x.MinValue)
            .NotNull().When(x => x.EventType == XpEventType.StreakMilestone)
            .WithMessage("Quy tắc mốc streak phải khai số ngày ở trường ngưỡng.");
    }
}

public sealed class SaveXpRuleHandler(IAppDbContext db)
    : ICommandHandler<SaveXpRuleCommand, Result<XpRuleDto>>
{
    public async Task<Result<XpRuleDto>> Handle(SaveXpRuleCommand request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<XpRuleDto>.NotFound("Không tìm thấy lớp học phần.");

        // Kiểm trùng TRƯỚC khi đụng vào change tracker: thêm entity rồi mới phát hiện trùng là
        // để lại một hàng Added treo trong context, và một SaveChanges của bước sau trong cùng
        // request sẽ ghi nó xuống dù nhánh này đã trả Conflict.
        //
        // Hai quy tắc cùng loại và cùng ngưỡng không làm sai kết quả — XpRuleEngine sắp xếp tất
        // định — nhưng làm bảng quy tắc không còn đọc được, nên vẫn chặn.
        var trung = await db.XpRules.AsNoTracking().AnyAsync(
            r => r.OfferingId == request.OfferingId
                 && r.Id != request.Id
                 && r.EventType == request.EventType
                 && r.MinValue == request.MinValue, ct);

        if (trung)
            return Result<XpRuleDto>.Conflict(
                "Đã có quy tắc cùng loại sự kiện và cùng ngưỡng cho lớp này.");

        XpRule quy;

        if (request.Id is { } id)
        {
            var tim = await db.XpRules
                .SingleOrDefaultAsync(r => r.Id == id && r.OfferingId == request.OfferingId, ct);

            if (tim is null) return Result<XpRuleDto>.NotFound("Không tìm thấy quy tắc XP.");
            quy = tim;
        }
        else
        {
            quy = new XpRule
            {
                OfferingId = request.OfferingId,
                EventType = request.EventType,
                Points = request.Points,
            };

            db.XpRules.Add(quy);
        }

        quy.EventType = request.EventType;
        quy.Points = request.Points;
        quy.MinValue = request.MinValue;
        quy.DailyCap = request.DailyCap;
        quy.IsEnabled = request.IsEnabled;

        await db.SaveChangesAsync(ct);

        return Result<XpRuleDto>.Ok(XpRuleDto.From(quy));
    }
}

// ── Xóa một quy tắc ─────────────────────────────────────────────────────────────────────

public sealed record DeleteXpRuleCommand(Guid Id, Guid InstructorId) : ICommand<Result>;

public sealed class DeleteXpRuleHandler(IAppDbContext db) : ICommandHandler<DeleteXpRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteXpRuleCommand request, CancellationToken ct)
    {
        // Điều kiện sở hữu nằm TRONG câu truy vấn: không tìm thấy ⇒ 404, không bao giờ 403.
        var quy = await (from r in db.XpRules
                         where r.Id == request.Id
                         join o in db.CourseOfferings on r.OfferingId equals o.Id
                         join s in db.Subjects on o.SubjectId equals s.Id
                         where s.InstructorId == request.InstructorId
                         select r).SingleOrDefaultAsync(ct);

        if (quy is null) return Result.NotFound("Không tìm thấy quy tắc XP.");

        // Sự kiện XP đã ghi KHÔNG bị xóa theo: chúng là lịch sử giải thích vì sao sinh viên có
        // số XP hiện tại. Xóa quy tắc chỉ dừng việc cộng thêm từ nay về sau.
        db.XpRules.Remove(quy);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}

// ── Gieo bảng quy tắc mặc định cho lớp mới ──────────────────────────────────────────────

/// <summary>
/// Một lớp mới mở có bảng quy tắc TRỐNG, nghĩa là không ai được XP nào và cơ chế nhìn như hỏng.
/// Lệnh này gieo bộ mặc định để giảng viên có cái để sửa thay vì có một trang trắng.
/// </summary>
public sealed record SeedXpRulesCommand(Guid OfferingId) : ICommand<Result<List<XpRuleDto>>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SeedXpRulesHandler(IAppDbContext db)
    : ICommandHandler<SeedXpRulesCommand, Result<List<XpRuleDto>>>
{
    public async Task<Result<List<XpRuleDto>>> Handle(SeedXpRulesCommand request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<List<XpRuleDto>>.NotFound("Không tìm thấy lớp học phần.");

        // Gieo đè lên bảng đã có là ghi đè cấu hình giảng viên đã tinh chỉnh cả kỳ, nên chặn.
        if (await db.XpRules.AnyAsync(r => r.OfferingId == request.OfferingId, ct))
            return Result<List<XpRuleDto>>.Conflict(
                "Lớp này đã có bảng quy tắc XP. Xóa hết quy tắc hiện có trước nếu muốn gieo lại.");

        var moi = XpRuleEngine.SeedRules()
            .Select(s => new XpRule
            {
                OfferingId = request.OfferingId,
                EventType = s.EventType,
                Points = s.Points,
                MinValue = s.MinValue,
                DailyCap = s.DailyCap,
                IsEnabled = s.IsEnabled,
            })
            .ToList();

        db.XpRules.AddRange(moi);
        await db.SaveChangesAsync(ct);

        return Result<List<XpRuleDto>>.Ok([.. moi.Select(XpRuleDto.From)]);
    }
}
