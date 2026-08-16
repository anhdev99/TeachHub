namespace TeachHub.Domain.Gamification;

/// <summary>
/// The measurable metrics of one learner within ONE class. Badge conditions may only talk about
/// the metrics listed here — which is what makes adding a new badge a new ROW in a table rather
/// than a deploy. ADD values at the end, never insert in the middle.
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

/// <summary>One condition declared as data — stored in the jsonb column of <c>badges</c>.</summary>
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

        // Adding a value to BadgeMetric and forgetting the arm here has to blow up the moment it
        // is tried, instead of silently returning 0 — returning 0 means every AtMost condition on
        // that metric is always true and the badge gets awarded to the entire class.
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Chỉ số huy hiệu chưa được hỗ trợ."),
    };
}

/// <param name="Conditions">Empty means NOT yet declared — the badge is never awarded (see <see cref="BadgeEvaluator.IsEarned"/>).</param>
public sealed record BadgeSpec(
    Guid Id, string Code, BadgeLogic Logic, IReadOnlyList<BadgeCondition> Conditions);

public static class BadgeEvaluator
{
    public static bool IsEarned(BadgeSpec spec, LearnerMetrics metrics)
    {
        // FAIL CLOSED. An empty list satisfies "all conditions hold" under formal logic, so the
        // most natural way to write this — Conditions.All(...) — would award the badge to EVERY
        // student the moment an instructor saves a half-finished badge. And a badge that has been
        // awarded cannot be taken back without losing face in front of the whole class.
        if (spec.Conditions.Count == 0) return false;

        return spec.Logic == BadgeLogic.Any
            ? spec.Conditions.Any(c => Satisfies(c, metrics))
            : spec.Conditions.All(c => Satisfies(c, metrics));
    }

    private static bool Satisfies(BadgeCondition condition, LearnerMetrics metrics)
    {
        var value = metrics.ValueOf(condition.Metric);

        return condition.Comparison switch
        {
            BadgeComparison.AtLeast => value >= condition.Threshold,
            BadgeComparison.AtMost => value <= condition.Threshold,
            BadgeComparison.Exactly => value == condition.Threshold,
            _ => false,
        };
    }

    /// <summary>Badges just earned that have NEVER been awarded before — the source of new <c>earned_badges</c> rows.</summary>
    public static IReadOnlyList<BadgeSpec> NewlyEarned(
        IEnumerable<BadgeSpec> catalog, LearnerMetrics metrics, IReadOnlySet<Guid> alreadyEarned) =>
        [.. catalog.Where(b => !alreadyEarned.Contains(b.Id) && IsEarned(b, metrics))];

    /// <summary>
    /// The number of CONSECUTIVE on-time submissions up to the most recent one.
    /// <paramref name="onTimeInChronologicalOrder"/> must follow the actual submission order in
    /// time: one late submission in the middle breaks the run, and without that "consecutive"
    /// would just mean "total" and the badge condition would lose all meaning.
    /// </summary>
    public static int CurrentOnTimeRun(IEnumerable<bool> onTimeInChronologicalOrder)
    {
        var run = 0;

        foreach (var onTime in onTimeInChronologicalOrder)
            run = onTime ? run + 1 : 0;

        return run;
    }

    public static int LongestOnTimeRun(IEnumerable<bool> onTimeInChronologicalOrder)
    {
        var run = 0;
        var longest = 0;

        foreach (var onTime in onTimeInChronologicalOrder)
        {
            run = onTime ? run + 1 : 0;
            if (run > longest) longest = run;
        }

        return longest;
    }
}
