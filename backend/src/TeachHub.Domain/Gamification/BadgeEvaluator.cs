namespace TeachHub.Domain.Gamification;

/// <summary>
/// Chỉ số đo được của một người học trong MỘT lớp. Điều kiện huy hiệu chỉ được nói tới các
/// chỉ số ở đây — nhờ vậy thêm một huy hiệu mới là thêm một HÀNG trong bảng, không phải một
/// lần deploy. THÊM giá trị vào cuối, không chèn giữa.
/// </summary>
public enum BadgeMetric
{
    TotalXp,
    CurrentStreakDays,
    LongestStreakDays,
    LessonsCompleted,
    OnTimeSubmissions,
    LateSubmissions,
    ConsecutiveOnTimeSubmissions,
    HighScoreQuizzes,
}

public enum BadgeComparison { AtLeast, AtMost, Exactly }

public enum BadgeLogic { All, Any }

/// <summary>Một điều kiện khai báo dạng dữ liệu — lưu trong cột jsonb của <c>badges</c>.</summary>
public sealed class BadgeCondition
{
    public required BadgeMetric Metric { get; set; }
    public BadgeComparison Comparison { get; set; } = BadgeComparison.AtLeast;
    public required decimal Threshold { get; set; }

    public BadgeCondition DeepCopy() =>
        new() { Metric = Metric, Comparison = Comparison, Threshold = Threshold };
}

public sealed record LearnerMetrics
{
    public int TotalXp { get; init; }
    public int CurrentStreakDays { get; init; }
    public int LongestStreakDays { get; init; }
    public int LessonsCompleted { get; init; }
    public int OnTimeSubmissions { get; init; }
    public int LateSubmissions { get; init; }
    public int ConsecutiveOnTimeSubmissions { get; init; }
    public int HighScoreQuizzes { get; init; }

    public decimal ValueOf(BadgeMetric metric) => metric switch
    {
        BadgeMetric.TotalXp => TotalXp,
        BadgeMetric.CurrentStreakDays => CurrentStreakDays,
        BadgeMetric.LongestStreakDays => LongestStreakDays,
        BadgeMetric.LessonsCompleted => LessonsCompleted,
        BadgeMetric.OnTimeSubmissions => OnTimeSubmissions,
        BadgeMetric.LateSubmissions => LateSubmissions,
        BadgeMetric.ConsecutiveOnTimeSubmissions => ConsecutiveOnTimeSubmissions,
        BadgeMetric.HighScoreQuizzes => HighScoreQuizzes,

        // Thêm giá trị vào BadgeMetric mà quên nhánh ở đây phải nổ ngay lúc thử, chứ không
        // âm thầm trả 0 — trả 0 nghĩa là mọi điều kiện AtMost trên chỉ số đó luôn đúng và
        // huy hiệu được trao cho cả lớp.
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Chỉ số huy hiệu chưa được hỗ trợ."),
    };
}

/// <param name="Conditions">Rỗng nghĩa là CHƯA khai — huy hiệu không bao giờ được trao (xem <see cref="BadgeEvaluator.IsEarned"/>).</param>
public sealed record BadgeSpec(
    Guid Id, string Code, BadgeLogic Logic, IReadOnlyList<BadgeCondition> Conditions);

public static class BadgeEvaluator
{
    public static bool IsEarned(BadgeSpec spec, LearnerMetrics metrics)
    {
        // ĐÓNG THEO HƯỚNG AN TOÀN. Danh sách rỗng thỏa mãn "mọi điều kiện đều đúng" theo logic
        // hình thức, nên cách viết tự nhiên nhất — Conditions.All(...) — sẽ trao huy hiệu cho
        // TOÀN BỘ sinh viên ngay khi giảng viên bấm lưu một huy hiệu còn dở dang. Huy hiệu đã
        // trao thì không thu lại được mà không mất mặt với cả lớp.
        if (spec.Conditions.Count == 0) return false;

        return spec.Logic == BadgeLogic.Any
            ? spec.Conditions.Any(c => Satisfies(c, metrics))
            : spec.Conditions.All(c => Satisfies(c, metrics));
    }

    private static bool Satisfies(BadgeCondition condition, LearnerMetrics metrics)
    {
        var giaTri = metrics.ValueOf(condition.Metric);

        return condition.Comparison switch
        {
            BadgeComparison.AtLeast => giaTri >= condition.Threshold,
            BadgeComparison.AtMost => giaTri <= condition.Threshold,
            BadgeComparison.Exactly => giaTri == condition.Threshold,
            _ => false,
        };
    }

    /// <summary>Huy hiệu vừa đạt mà CHƯA từng được trao — nguồn của các hàng <c>earned_badges</c> mới.</summary>
    public static IReadOnlyList<BadgeSpec> NewlyEarned(
        IEnumerable<BadgeSpec> catalog, LearnerMetrics metrics, IReadOnlySet<Guid> alreadyEarned) =>
        [.. catalog.Where(b => !alreadyEarned.Contains(b.Id) && IsEarned(b, metrics))];

    /// <summary>
    /// Số lần nộp đúng hạn LIÊN TIẾP tính tới lần nộp gần nhất. <paramref name="onTimeInChronologicalOrder"/>
    /// phải theo đúng thứ tự thời gian nộp: một lần trễ ở giữa cắt chuỗi, nếu không thì
    /// "liên tiếp" chỉ còn nghĩa là "tổng cộng" và điều kiện huy hiệu mất hết ý nghĩa.
    /// </summary>
    public static int CurrentOnTimeRun(IEnumerable<bool> onTimeInChronologicalOrder)
    {
        var chuoi = 0;

        foreach (var dungHan in onTimeInChronologicalOrder)
            chuoi = dungHan ? chuoi + 1 : 0;

        return chuoi;
    }

    public static int LongestOnTimeRun(IEnumerable<bool> onTimeInChronologicalOrder)
    {
        var chuoi = 0;
        var daiNhat = 0;

        foreach (var dungHan in onTimeInChronologicalOrder)
        {
            chuoi = dungHan ? chuoi + 1 : 0;
            if (chuoi > daiNhat) daiNhat = chuoi;
        }

        return daiNhat;
    }
}
