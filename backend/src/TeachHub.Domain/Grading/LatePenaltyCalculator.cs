namespace TeachHub.Domain.Grading;

/// <param name="PercentDeducted">
/// The percentage ACTUALLY deducted, already clamped at 100%. It does not report the raw 150%:
/// that would suggest the score can go negative, which the system never allows to happen.
/// </param>
public sealed record LatePenalty(
    int DaysLate, decimal PercentDeducted, decimal PenaltyPoints, decimal FinalScore);

/// <summary>
/// Late-submission penalty driven by <c>Assignment.LatePenaltyPercentPerDay</c>. A pure function:
/// it takes the timestamps from outside (the calling layer's TimeProvider) instead of reading
/// <c>DateTimeOffset.UtcNow</c> — otherwise testing a "3 days late" case would mean waiting three
/// real days.
/// </summary>
public static class LatePenaltyCalculator
{
    /// <summary>
    /// Days late, ROUNDED UP — one minute late is still a full day late. Round down instead and
    /// the whole class submits at 00:05 and nobody is penalized at all, which means the deadline
    /// no longer means anything.
    /// </summary>
    public static int DaysLate(DateTimeOffset? dueAt, DateTimeOffset? submittedAt)
    {
        if (dueAt is not { } due || submittedAt is not { } submitted) return 0;

        var late = submitted - due;
        if (late <= TimeSpan.Zero) return 0;

        // Computed in INTEGER ticks, not with Math.Ceiling(late.TotalDays).
        // TotalDays is a double: an exact 24 hours can come out as 1.0000000000000002 depending on
        // the subtraction, and then a student who submits exactly 24 hours after the deadline is
        // penalized twice as hard as one who submits a second later.
        var (wholeDays, remainder) = Math.DivRem(late.Ticks, TimeSpan.TicksPerDay);

        return (int)(wholeDays + (remainder > 0 ? 1 : 0));
    }

    public static LatePenalty Apply(
        decimal score, decimal percentPerDay, DateTimeOffset? dueAt, DateTimeOffset? submittedAt)
    {
        var days = DaysLate(dueAt, submittedAt);

        // Clamping to [0, 100] IS what keeps the score from ever going negative. 30%/day × 5 days
        // = 150%, and a negative column score would drag the final total below that of a student
        // who submitted nothing at all.
        var percent = Math.Clamp(percentPerDay * days, 0m, 100m);

        var finalScore = Math.Round(
            Math.Max(0m, score * (1m - percent / 100m)), 2, MidpointRounding.AwayFromZero);

        return new LatePenalty(
            days, percent, Math.Round(score - finalScore, 2, MidpointRounding.AwayFromZero), finalScore);
    }
}
