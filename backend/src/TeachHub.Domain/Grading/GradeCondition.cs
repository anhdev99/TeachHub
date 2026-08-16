namespace TeachHub.Domain.Grading;

public enum ConditionOperator { LessThan, LessOrEqual }

public enum CapAction { CapFinalAt }

/// <summary>
/// A class's minimum-score condition (jsonb on CourseOffering).
/// A real example: "final exam &lt; 4.0 → total score capped at 3.9".
/// </summary>
public sealed class GradeCondition
{
    /// <summary>Links by the column's KEY, not by Id — the condition keeps its meaning when the column is replaced.</summary>
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
/// <c>Score</c> = the score after the conditions have been applied. <c>UnknownColumnKeys</c> =
/// conditions pointing at a column that does not exist: they are skipped, but a warning MUST be
/// shown, because staying silent here means the subject's minimum-score rule is not in force and
/// nobody knows it.
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

        var unknownKeys = new List<string>();
        decimal? cap = null;

        foreach (var condition in conditions)
        {
            var column = columns.FirstOrDefault(c => c.Key == condition.ColumnKey);
            if (column is null)
            {
                unknownKeys.Add(condition.ColumnKey);
                continue;
            }

            var columnScore = columnScores.TryGetValue(column.Id, out var v) ? v : 0m;

            var triggered = condition.Op switch
            {
                ConditionOperator.LessThan => columnScore < condition.Threshold,
                ConditionOperator.LessOrEqual => columnScore <= condition.Threshold,
                _ => false,
            };

            if (!triggered) continue;

            // Several conditions triggering at once → take the LOWEST cap. A minimum-score rule
            // is a requirement to be met, not a menu you get to pick the mildest option from.
            cap = cap is { } existing ? Math.Min(existing, condition.ActionValue) : condition.ActionValue;
        }

        return new(cap is { } capValue ? Math.Min(total, capValue) : total, unknownKeys);
    }
}
