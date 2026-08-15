using TeachHub.Domain.Common;

namespace TeachHub.Domain.Assignments;

/// <summary>
/// Rubric thuộc về GIẢNG VIÊN chứ không thuộc về một bài tập (Gap #18): cùng một bộ tiêu chí
/// "chạy đúng / xử lý lỗi / code sạch" dùng lại được cho mọi bài lập trình của mọi môn.
/// Buộc rubric thuộc bài tập là bắt gõ lại cùng ba tiêu chí đó hàng chục lần mỗi học kỳ.
/// </summary>
public class Rubric : Entity
{
    public required string Name { get; set; }
    public required Guid InstructorId { get; set; }
    public bool IsTemplate { get; set; }
    public List<RubricCriterion> Criteria { get; set; } = [];

    public decimal MaxPoints => Criteria.Sum(c => c.MaxPoints);

    /// <summary>
    /// scores: criterion_id → điểm. Tiêu chí thiếu tính 0 vì chấm dở dang rồi lưu nháp là
    /// hành vi bình thường, không phải lỗi. Toàn bộ tính toán dùng decimal — điểm số không
    /// bao giờ được đi qua double.
    /// </summary>
    public decimal Score(IReadOnlyDictionary<Guid, decimal> scores)
    {
        decimal tong = 0;

        foreach (var c in Criteria)
        {
            if (!scores.TryGetValue(c.Id, out var d)) continue;

            if (d < 0 || d > c.MaxPoints)
                throw new ArgumentOutOfRangeException(nameof(scores),
                    $"Điểm tiêu chí '{c.Description}' phải nằm trong [0, {c.MaxPoints}], nhận được {d}.");

            tong += d;
        }

        return Math.Round(tong, 2, MidpointRounding.AwayFromZero);
    }

    public Rubric CloneForInstructor(Guid instructorId) => new()
    {
        Name = Name,
        InstructorId = instructorId,
        IsTemplate = false,
        Criteria = [.. Criteria.Select(c => c.Clone())],
    };
}

public class RubricCriterion : Entity
{
    public Guid RubricId { get; set; }
    public required string Description { get; set; }
    public required decimal MaxPoints { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>Các mức mô tả (Xuất sắc / Khá / Trung bình / Yếu) — lưu jsonb.</summary>
    public List<CriterionLevel> Levels { get; set; } = [];

    public RubricCriterion Clone() => new()
    {
        Description = Description,
        MaxPoints = MaxPoints,
        OrderIndex = OrderIndex,
        Levels = [.. Levels.Select(l => l.Clone())],
    };
}

public class CriterionLevel
{
    public required string Label { get; set; }
    public required decimal Points { get; set; }
    public string? Description { get; set; }

    public CriterionLevel Clone() => new()
    {
        Label = Label, Points = Points, Description = Description,
    };
}
