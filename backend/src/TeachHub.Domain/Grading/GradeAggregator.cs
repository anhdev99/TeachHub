namespace TeachHub.Domain.Grading;

/// <summary>How scores are combined WITHIN a single column. Picked from a list, never a hand-typed formula.</summary>
public enum AggregationKind
{
    Manual,            // fully hand-entered, no automatic aggregation
    Average,           // arithmetic mean of the sources
    WeightedAverage,   // uses GradeColumnSource.WeightWithinColumn
    Sum,               // add them up, then clamp to [0, 10]
    Max,
    Min,
    DropLowestN,       // drop the N lowest scores, then average
    BestNOfM,          // take the N best submissions, then average
}

/// <summary>Stored as jsonb on GradeColumn. No free-form expressions, so the syntax can never be typed wrong.</summary>
public sealed class GradeAggregation
{
    public AggregationKind Kind { get; set; } = AggregationKind.Average;

    /// <summary>Number of scores dropped (DropLowestN) or kept (BestNOfM). Every other Kind ignores it.</summary>
    public int N { get; set; }

    /// <summary>An ungraded submission counts as 0 (the default, and the right call at end of term) or is excluded from the denominator.</summary>
    public bool TreatMissingAsZero { get; set; } = true;
}

/// <summary>
/// One ALREADY-FETCHED score source for one student. <paramref name="SourceMaxScore"/> is the
/// source's own scale (a rubric totalling 20 points means 20) — it is required for normalization.
/// </summary>
public sealed record SourceScore(decimal? RawScore, decimal SourceMaxScore, decimal? WeightWithinColumn = null);

/// <summary>
/// Pure functions, no I/O. Every rule for combining scores within a column lives here.
/// EVERY source is normalized to the 0–10 scale BEFORE being combined — a 16 on a rubric scored
/// out of 20 enters the column as 8.0, not as a 16 that then gets clamped down to 10.
/// </summary>
public static class GradeAggregator
{
    public static decimal Apply(GradeAggregation rule, IReadOnlyList<SourceScore> sources, decimal maxScore = 10m)
    {
        // A Manual column never comes through here in the real flow — its score is read straight
        // off GradeRecord. Return 0 so that a mistaken call shows an obvious 0 rather than a
        // number that merely looks plausible.
        if (rule.Kind == AggregationKind.Manual) return 0m;

        var items = Normalize(rule, sources, maxScore);
        if (items.Count == 0) return 0m;                 // no sources at all → 0, and NO division by zero

        var values = items.Select(i => i.Value).ToList();

        var result = rule.Kind switch
        {
            AggregationKind.Average => values.Sum() / values.Count,
            AggregationKind.WeightedAverage => ComputeWeightedAverage(items),
            AggregationKind.Sum => values.Sum(),
            AggregationKind.Max => values.Max(),
            AggregationKind.Min => values.Min(),
            AggregationKind.DropLowestN => AverageDroppingLowest(values, rule.N),
            AggregationKind.BestNOfM => AverageOfBest(values, rule.N),
            _ => 0m,
        };

        return Math.Clamp(result, 0m, maxScore);
    }

    /// <summary>
    /// Normalizes every source onto the 0–10 scale. An assignment carries its own MaxScore (a
    /// rubric total may well be 20): combining the raw scores directly and clamping at 10 yields
    /// a wrong grade, and wrong in the student's favour.
    /// </summary>
    private static List<(decimal Value, decimal Weight)> Normalize(
        GradeAggregation rule, IReadOnlyList<SourceScore> sources, decimal maxScore)
    {
        var result = new List<(decimal Value, decimal Weight)>(sources.Count);

        foreach (var source in sources)
        {
            var weight = source.WeightWithinColumn ?? 1m;

            if (source.RawScore is not { } raw)
            {
                if (rule.TreatMissingAsZero) result.Add((0m, weight));
                continue;                                 // false → dropped from numerator and denominator alike
            }

            var sourceMax = source.SourceMaxScore > 0m ? source.SourceMaxScore : maxScore;
            result.Add((Math.Clamp(raw / sourceMax * maxScore, 0m, maxScore), weight));
        }

        return result;
    }

    private static decimal ComputeWeightedAverage(List<(decimal Value, decimal Weight)> items)
    {
        var totalWeight = items.Sum(i => i.Weight);

        return totalWeight <= 0m
            ? items.Sum(i => i.Value) / items.Count       // all weights left blank = split evenly
            : items.Sum(i => i.Value * i.Weight) / totalWeight;
    }

    private static decimal AverageDroppingLowest(List<decimal> values, int n)
    {
        // Keep at least 1 score: an N ≥ the number of sources must NOT drop them all and then divide by zero.
        var dropCount = Math.Clamp(n, 0, values.Count - 1);
        var kept = values.Order().Skip(dropCount).ToList();
        return kept.Sum() / kept.Count;
    }

    private static decimal AverageOfBest(List<decimal> values, int n)
    {
        var takeCount = Math.Clamp(n, 1, values.Count);
        var top = values.OrderDescending().Take(takeCount).ToList();
        return top.Sum() / top.Count;
    }
}
