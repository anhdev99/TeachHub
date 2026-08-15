using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Assignments;

namespace TeachHub.Application.Features.Rubrics;

public sealed record CriterionLevelDto(string Label, decimal Points, string? Description);

public sealed record RubricCriterionDto(
    Guid Id, string Description, decimal MaxPoints, int OrderIndex, List<CriterionLevelDto> Levels);

public sealed record RubricDto(
    Guid Id, string Name, bool IsTemplate, decimal MaxPoints, List<RubricCriterionDto> Criteria)
{
    public static RubricDto From(Rubric r) => new(
        r.Id, r.Name, r.IsTemplate, r.MaxPoints,
        [.. r.Criteria.OrderBy(c => c.OrderIndex).Select(c => new RubricCriterionDto(
            c.Id, c.Description, c.MaxPoints, c.OrderIndex,
            [.. c.Levels.Select(l => new CriterionLevelDto(l.Label, l.Points, l.Description))]))]);
}

// ── Danh sách rubric của giảng viên ─────────────────────────────────────────────────────

public sealed record GetRubricsQuery(Guid InstructorId) : IQuery<List<RubricDto>>;

public sealed class GetRubricsHandler(IAppDbContext db) : IQueryHandler<GetRubricsQuery, List<RubricDto>>
{
    public async Task<List<RubricDto>> Handle(GetRubricsQuery request, CancellationToken ct)
    {
        // Rubric thuộc GIẢNG VIÊN chứ không thuộc môn (Gap #18): cùng một bộ tiêu chí dùng
        // lại được cho mọi bài lập trình của mọi môn. Lọc instructor_id ngay trong truy vấn.
        var ds = await db.Rubrics.AsNoTracking()
            .Include(r => r.Criteria)
            .Where(r => r.InstructorId == request.InstructorId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return [.. ds.Select(RubricDto.From)];
    }
}

// ── Tạo rubric ──────────────────────────────────────────────────────────────────────────

public sealed record NewCriterion(string Description, decimal MaxPoints);

public sealed record CreateRubricCommand(string Name, bool IsTemplate, List<NewCriterion> Criteria)
    : ICommand<Result<RubricDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class CreateRubricValidator : AbstractValidator<CreateRubricCommand>
{
    public CreateRubricValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa đặt tên rubric.").MaximumLength(300);
        RuleFor(x => x.Criteria).NotEmpty().WithMessage("Rubric phải có ít nhất một tiêu chí.");

        RuleForEach(x => x.Criteria).ChildRules(c =>
        {
            c.RuleFor(y => y.Description).NotEmpty().WithMessage("Tiêu chí chưa có mô tả.").MaximumLength(500);
            c.RuleFor(y => y.MaxPoints).GreaterThan(0m).WithMessage("Điểm tối đa của tiêu chí phải lớn hơn 0.");
        });

        // Tổng trọng số là luật thuần hình dạng trên chính payload (cả bộ tiêu chí gửi trong
        // một request nên nó tự đủ dữ liệu) ⇒ thuộc validator, không phải handler.
        RuleFor(x => x.Criteria)
            .Must(c => c.Sum(y => y.MaxPoints) <= 10m)
            .WithMessage("Tổng điểm các tiêu chí vượt quá 10. Rubric tổng 12 điểm cho bài thang 10 "
                       + "là lỗi chỉ lộ ra lúc đang chấm dở.");
    }
}

public sealed class CreateRubricHandler(IAppDbContext db)
    : ICommandHandler<CreateRubricCommand, Result<RubricDto>>
{
    public async Task<Result<RubricDto>> Handle(CreateRubricCommand request, CancellationToken ct)
    {
        if (await db.Rubrics.AnyAsync(
                r => r.InstructorId == request.InstructorId && r.Name == request.Name.Trim(), ct))
            return Result<RubricDto>.Conflict($"Bạn đã có rubric tên “{request.Name.Trim()}”.");

        var rubric = new Rubric
        {
            Name = request.Name.Trim(),
            InstructorId = request.InstructorId,
            IsTemplate = request.IsTemplate,
            Criteria = [.. request.Criteria.Select((c, i) => new RubricCriterion
            {
                Description = c.Description.Trim(),
                MaxPoints = c.MaxPoints,
                OrderIndex = i,
            })],
        };

        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync(ct);

        return Result<RubricDto>.Ok(RubricDto.From(rubric));
    }
}
