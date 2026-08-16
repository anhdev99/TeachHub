using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

// ── Thư viện huy hiệu của giảng viên ────────────────────────────────────────────────────

public sealed record GetBadgesQuery(Guid InstructorId) : IQuery<List<BadgeDto>>;

public sealed class GetBadgesHandler(IAppDbContext db) : IQueryHandler<GetBadgesQuery, List<BadgeDto>>
{
    public async Task<List<BadgeDto>> Handle(GetBadgesQuery request, CancellationToken ct)
    {
        var ds = await db.Badges.AsNoTracking()
            .Where(b => b.InstructorId == request.InstructorId)
            .OrderBy(b => b.Code)
            .ToListAsync(ct);

        return [.. ds.Select(BadgeDto.From)];
    }
}

// ── Tạo / sửa huy hiệu ──────────────────────────────────────────────────────────────────

public sealed record SaveBadgeCommand(
    Guid? Id, string Code, string Name, string? Description, string? Icon,
    BadgeLogic Logic, bool IsEnabled, List<BadgeConditionDto> Conditions)
    : ICommand<Result<BadgeDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveBadgeValidator : AbstractValidator<SaveBadgeCommand>
{
    public SaveBadgeValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Chưa nhập mã huy hiệu.")
            .MaximumLength(64)
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Mã huy hiệu chỉ gồm chữ thường không dấu, số và dấu gạch dưới.");

        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa nhập tên huy hiệu.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Icon).MaximumLength(16);

        // Điều kiện rỗng ⇒ huy hiệu không bao giờ được trao (BadgeEvaluator đóng theo hướng an
        // toàn). Chặn ngay từ đây để giảng viên không tạo ra một huy hiệu chết mà không hiểu
        // vì sao cả lớp không ai đạt — triệu chứng trùng khít với "cơ chế bị hỏng".
        RuleFor(x => x.Conditions)
            .Must(c => c.Count > 0)
            .WithMessage("Huy hiệu phải có ít nhất một điều kiện, nếu không sẽ không ai đạt được.");

        RuleForEach(x => x.Conditions).ChildRules(c =>
            c.RuleFor(x => x.Threshold)
             .GreaterThanOrEqualTo(0m).WithMessage("Ngưỡng của điều kiện không được âm."));
    }
}

public sealed class SaveBadgeHandler(IAppDbContext db) : ICommandHandler<SaveBadgeCommand, Result<BadgeDto>>
{
    public async Task<Result<BadgeDto>> Handle(SaveBadgeCommand request, CancellationToken ct)
    {
        var ma = request.Code.Trim();

        var trungMa = await db.Badges.AsNoTracking().AnyAsync(
            b => b.InstructorId == request.InstructorId && b.Code == ma && b.Id != request.Id, ct);

        if (trungMa) return Result<BadgeDto>.Conflict($"Đã có huy hiệu mang mã “{ma}”.");

        Badge hh;

        if (request.Id is { } id)
        {
            var tim = await db.Badges
                .SingleOrDefaultAsync(b => b.Id == id && b.InstructorId == request.InstructorId, ct);

            if (tim is null) return Result<BadgeDto>.NotFound("Không tìm thấy huy hiệu.");
            hh = tim;
        }
        else
        {
            hh = new Badge { InstructorId = request.InstructorId, Code = ma, Name = request.Name.Trim() };
            db.Badges.Add(hh);
        }

        hh.Code = ma;
        hh.Name = request.Name.Trim();
        hh.Description = request.Description?.Trim();
        hh.Icon = request.Icon?.Trim();
        hh.Logic = request.Logic;
        hh.IsEnabled = request.IsEnabled;

        // Conditions là owned collection map thẳng vào cột jsonb, không phải bảng riêng: gán
        // danh sách mới là thay cả cột, không cần Remove/Add từng hàng qua DbSet.
        hh.Conditions = [.. request.Conditions.Select(c => new BadgeCondition
        {
            Metric = c.Metric, Comparison = c.Comparison, Threshold = c.Threshold,
        })];

        await db.SaveChangesAsync(ct);

        return Result<BadgeDto>.Ok(BadgeDto.From(hh));
    }
}

// ── Xóa huy hiệu ────────────────────────────────────────────────────────────────────────

public sealed record DeleteBadgeCommand(Guid Id, Guid InstructorId) : ICommand<Result>;

public sealed class DeleteBadgeHandler(IAppDbContext db) : ICommandHandler<DeleteBadgeCommand, Result>
{
    public async Task<Result> Handle(DeleteBadgeCommand request, CancellationToken ct)
    {
        var hh = await db.Badges
            .SingleOrDefaultAsync(b => b.Id == request.Id && b.InstructorId == request.InstructorId, ct);

        if (hh is null) return Result.NotFound("Không tìm thấy huy hiệu.");

        // Huy hiệu đã trao cho sinh viên thì xóa là lấy lại một thứ đã công khai trao — sinh
        // viên có ảnh chụp màn hình, và không lời giải thích nào nghe lọt tai. Tắt thì được.
        var soLan = await db.EarnedBadges.CountAsync(x => x.BadgeId == request.Id, ct);

        if (soLan > 0)
            return Result.Conflict(
                $"Huy hiệu này đã được trao cho {soLan} sinh viên. Hãy tắt nó thay vì xóa.");

        db.Badges.Remove(hh);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
