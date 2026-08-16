namespace TeachHub.Domain.Gamification;

/// <summary>
/// The kinds of event that earn XP. This enum is the contract between the rule table (data) and
/// the places that detect events (handlers) — ADD values at the END, never insert in the middle:
/// it is persisted to the DB as a STRING so inserting in the middle does not corrupt data, but it
/// does change the meaning of every list rendered in declaration order in the UI.
/// </summary>
public enum XpEventType
{
    LessonCompleted,
    ComponentCompleted,
    AssignmentSubmittedOnTime,
    QuizHighScore,
    StreakMilestone,
}

/// <summary>
/// ONE row of the XP rule table — it is DATA, not code. An instructor can change the points and
/// the thresholds mid-term without a deploy; that is the entire reason the engine must not contain
/// a single number of its own.
/// </summary>
public sealed class XpRuleSpec
{
    public required XpEventType EventType { get; set; }

    /// <summary>XP awarded when this rule applies. Always positive — there is no negative XP in the MVP.</summary>
    public required int Points { get; set; }

    /// <summary>
    /// Minimum <see cref="XpEventInput.Value"/> for the rule to take effect, read as
    /// "at least" (&gt;=). null = the value is not considered. Example: a quiz scoring 8.0 or above.
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>Maximum number of XP awards for this event type within ONE day. null = no cap.</summary>
    public int? DailyCap { get; set; }

    /// <summary>
    /// Disabling a rule has to take effect immediately mid-term WITHOUT deleting it: deleting it
    /// also destroys the explanation of why a student was ever awarded under that rule.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <param name="Value">
/// The value carried by the event, compared against the threshold — a quiz score normalized onto
/// the 0–10 scale, a streak length in days, and so on. Always <c>decimal</c>, never <c>double</c>:
/// every number compared against a score threshold in this system is a decimal, and mixing the
/// types opens the door to 7.9999999 failing to clear a threshold of 8.
/// </param>
/// <param name="AwardedTodayForType">
/// How many times this event type has ALREADY been awarded XP during the current local day. The
/// caller counts and passes it in — the engine is pure and issues no queries.
/// </param>
public sealed record XpEventInput(XpEventType Type, decimal? Value = null, int AwardedTodayForType = 0);

public sealed record XpAward(int Points, XpRuleSpec? Rule, string? SkipReason)
{
    public static XpAward None(string? reason = null) => new(0, null, reason);

    public bool IsAwarded => Points > 0;
}

/// <summary>
/// Turns (event + rule table) into an XP amount. Pure functions, no I/O, no reading the clock.
/// </summary>
public static class XpRuleEngine
{
    /// <summary>
    /// The SEED set for a new class — still data, written into the <c>xp_rules</c> table and
    /// editable afterwards. It exists only so that day one of a new class is not an empty table
    /// the instructor has no idea how to fill in. NO other function in the engine reads it.
    /// </summary>
    public static IReadOnlyList<XpRuleSpec> SeedRules() =>
    [
        new() { EventType = XpEventType.LessonCompleted, Points = 10, DailyCap = 10 },
        new() { EventType = XpEventType.ComponentCompleted, Points = 2, DailyCap = 20 },
        new() { EventType = XpEventType.AssignmentSubmittedOnTime, Points = 25 },
        new() { EventType = XpEventType.QuizHighScore, Points = 20, MinValue = 8m },
        new() { EventType = XpEventType.QuizHighScore, Points = 30, MinValue = 9m },
        new() { EventType = XpEventType.StreakMilestone, Points = 15, MinValue = 7m },
    ];

    public static XpAward Evaluate(XpEventInput input, IReadOnlyList<XpRuleSpec> rules)
    {
        // The HIGHEST satisfied threshold tier wins; tiers are NOT added together. A quiz scoring
        // 9.5 against a table of {≥8 → 20, ≥9 → 30} must get exactly 30, not 50 — summing is how a
        // two-tier table quietly turns into a three-tier one that nobody ever declared.
        //
        // ThenByDescending(Points) followed by ThenBy(EventType) makes the result DETERMINISTIC
        // when two rows share a threshold (the instructor hit save twice): without it the same
        // student reads two different numbers depending on the order the query happened to return.
        var applicable = rules
            .Where(r => r.IsEnabled && r.EventType == input.Type)
            .Where(r => r.MinValue is not { } threshold || (input.Value is { } v && v >= threshold))
            .OrderByDescending(r => r.MinValue ?? decimal.MinValue)
            .ThenByDescending(r => r.Points)
            .FirstOrDefault();

        if (applicable is null) return XpAward.None();

        if (applicable.DailyCap is { } cap && input.AwardedTodayForType >= cap)
            return XpAward.None($"Đã đạt trần {cap} lần cộng XP trong ngày cho loại sự kiện này.");

        return new XpAward(applicable.Points, applicable, null);
    }
}
