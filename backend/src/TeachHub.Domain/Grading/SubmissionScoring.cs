namespace TeachHub.Domain.Grading;

/// <param name="RawScore">The score the instructor gave, BEFORE any penalty.</param>
/// <param name="FinalScore">The effective score, after hints and after lateness. Always within [0, RawScore].</param>
public sealed record ScoreBreakdown(
    decimal RawScore,
    int DaysLate,
    decimal LatePenaltyPercent,
    int HintsUnlocked,
    decimal HintPenaltyPercent,
    decimal FinalScore)
{
    public decimal PenaltyPoints =>
        Math.Round(RawScore - FinalScore, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Combines the two Phase 6b penalties into a single effective score, together with a breakdown
/// that explains to the student "10 points − 20% for hints − 10% for lateness = 7.2".
///
/// ALWAYS computed from the ORIGINAL score, never stacked on top of an already-penalized one.
/// That is what makes this function safe to run repeatedly without changing the result
/// (idempotent) — a hard requirement, because instructors will press "recalculate" after editing
/// a deadline or a penalty rate. Stack the penalties and every press drops the score another
/// notch, silently, with nothing to warn anyone.
///
/// The two penalties are multiplied together (rather than summing the percentages and deducting
/// once) so that the order they are applied in does not change the result — which means there is
/// no "correct order" left to misremember.
/// </summary>
public static class SubmissionScoring
{
    public static ScoreBreakdown Apply(
        decimal rawScore,
        decimal latePenaltyPercentPerDay,
        DateTimeOffset? dueAt,
        DateTimeOffset? submittedAt,
        IEnumerable<decimal> hintPenaltyPercents)
    {
        var hint = HintPenaltyCalculator.Apply(rawScore, hintPenaltyPercents);

        var daysLate = LatePenaltyCalculator.DaysLate(dueAt, submittedAt);
        var latePercent = Math.Clamp(latePenaltyPercentPerDay * daysLate, 0m, 100m);

        // Round ONCE, at the end. Rounding after each step means rounding the same number twice,
        // and the accumulated error lands exactly on the second decimal place — which is where the
        // student adds it up by hand and spots it.
        var finalScore = Math.Round(
            Math.Max(0m, rawScore * (1m - hint.PercentDeducted / 100m) * (1m - latePercent / 100m)),
            2, MidpointRounding.AwayFromZero);

        return new ScoreBreakdown(
            rawScore, daysLate, latePercent, hint.HintsUnlocked, hint.PercentDeducted, finalScore);
    }
}
