namespace TeachHub.Domain.Grading;

public sealed record HintPenalty(
    int HintsUnlocked, decimal PercentDeducted, decimal PenaltyPoints, decimal FinalScore);

/// <summary>
/// Deducts points according to how many hints the student unlocked (Gap #11, SRS UC-09). A pure function.
///
/// It takes the list of PERCENTAGES FROZEN AT UNLOCK TIME (<c>HintUnlock.PenaltyPercentApplied</c>)
/// rather than hint ids it would then look the current penalty up from: if the instructor changes
/// the penalty after half the class has already unlocked a hint, those students must keep exactly
/// the terms they saw on screen when they pressed the button.
/// </summary>
public static class HintPenaltyCalculator
{
    public static HintPenalty Apply(decimal score, IEnumerable<decimal> unlockedPenaltyPercents)
    {
        var percents = unlockedPenaltyPercents.ToList();

        // Sum them up, then clamp at 100%: unlocking four 30% hints is 120%, and without the clamp
        // the score would come out negative.
        var percent = Math.Clamp(percents.Sum(), 0m, 100m);

        var finalScore = Math.Round(
            Math.Max(0m, score * (1m - percent / 100m)), 2, MidpointRounding.AwayFromZero);

        return new HintPenalty(
            percents.Count, percent, Math.Round(score - finalScore, 2, MidpointRounding.AwayFromZero), finalScore);
    }
}
