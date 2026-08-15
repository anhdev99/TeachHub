namespace TeachHub.Domain.Grading;

public enum ConditionOperator { LessThan, LessOrEqual }

public enum CapAction { CapFinalAt }

/// <summary>
/// Điều kiện điểm sàn của lớp (jsonb trên CourseOffering).
/// Ví dụ có thật: "Cuối môn &lt; 4.0 → điểm tổng tối đa 3.9".
/// </summary>
public sealed class GradeCondition
{
    /// <summary>Nối bằng KEY của cột, không bằng Id — đổi cột vẫn giữ được ý nghĩa điều kiện.</summary>
    public required string ColumnKey { get; set; }

    public ConditionOperator Op { get; set; } = ConditionOperator.LessThan;
    public required decimal Threshold { get; set; }
    public CapAction Action { get; set; } = CapAction.CapFinalAt;
    public required decimal ActionValue { get; set; }

    public GradeCondition DeepCopy() => new()
    {
        ColumnKey = ColumnKey, Op = Op, Threshold = Threshold,
        Action = Action, ActionValue = ActionValue,
    };
}

/// <summary>
/// <c>Score</c> = điểm sau khi áp điều kiện. <c>UnknownColumnKeys</c> = những điều kiện trỏ tới
/// cột không tồn tại: bị bỏ qua nhưng PHẢI hiện cảnh báo, vì im lặng ở đây nghĩa là quy định
/// điểm sàn của môn không có hiệu lực mà không ai biết.
/// </summary>
public sealed record ConditionOutcome(decimal Score, IReadOnlyList<string> UnknownColumnKeys);

public static class GradeConditionEvaluator
{
    public static ConditionOutcome Apply(
        decimal total,
        IReadOnlyList<GradeColumn> columns,
        IReadOnlyDictionary<Guid, decimal> columnScores,
        IReadOnlyList<GradeCondition> conditions)
    {
        if (conditions.Count == 0) return new(total, []);

        var khongTonTai = new List<string>();
        decimal? tran = null;

        foreach (var dk in conditions)
        {
            var cot = columns.FirstOrDefault(c => c.Key == dk.ColumnKey);
            if (cot is null)
            {
                khongTonTai.Add(dk.ColumnKey);
                continue;
            }

            var diemCot = columnScores.TryGetValue(cot.Id, out var v) ? v : 0m;

            var dinh = dk.Op switch
            {
                ConditionOperator.LessThan => diemCot < dk.Threshold,
                ConditionOperator.LessOrEqual => diemCot <= dk.Threshold,
                _ => false,
            };

            if (!dinh) continue;

            // Nhiều điều kiện cùng đúng → lấy mức chặn THẤP NHẤT. Quy định điểm sàn là điều
            // kiện cần, không phải điều kiện được chọn lấy cái dễ chịu nhất.
            tran = tran is { } daCo ? Math.Min(daCo, dk.ActionValue) : dk.ActionValue;
        }

        return new(tran is { } muc ? Math.Min(total, muc) : total, khongTonTai);
    }
}
