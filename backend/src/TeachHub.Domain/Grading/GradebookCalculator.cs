namespace TeachHub.Domain.Grading;

/// <summary>Where a bonus point lands: straight on the final total, or inside a grade column.</summary>
public enum BonusTarget { FinalTotal, GradeColumn }

/// <summary>
/// One bonus-point grant for ONE student, stripped of its business context (reason, grantor).
/// <c>Id</c> and <c>GrantedAt</c> must both be present: they are the deterministic sort key of BonusLedger.
/// </summary>
public sealed record BonusEntry(
    Guid Id, BonusTarget Target, Guid? ColumnId, decimal Points, DateTimeOffset GrantedAt);

public sealed record GradebookSettings
{
    public BonusTarget DefaultTarget { get; init; } = BonusTarget.GradeColumn;
    public Guid? DefaultColumnId { get; init; }
    public decimal? MaxBonusPerStudent { get; init; }   // null = no separate cap
    public decimal MaxScore { get; init; } = 10m;
    public int DecimalPlaces { get; init; } = 1;

    public static GradebookSettings Default => new();
}

public sealed record ValidationOutcome(bool IsValid, string? Error);

/// <summary>
/// Bonus points CLOSED OFF once and for all: deterministically ordered and already capped. It can
/// only be created through <see cref="Build"/>. Every scoring function takes this Ledger rather
/// than a raw list of bonuses — which is what keeps the gradebook cell and the final total from
/// drifting apart, and stops two reads from producing two different answers.
/// </summary>
public sealed record BonusLedger
{
    public required IReadOnlyDictionary<Guid, decimal> ByColumnId { get; init; }
    public decimal ToFinalTotal { get; init; }

    /// <summary>
    /// The effective total of bonus points after the cap, INCLUDING the part attached to columns.
    /// For tooltips and statistics — NOT the number shown in the gradebook's bonus-points
    /// ("Điểm cộng") column.
    ///
    /// That bonus-points column, both on screen and in the Excel file, shows only
    /// <see cref="ToFinalTotal"/>: the part added into a column is already baked into that
    /// column's own number, so showing it again double-counts it, and an instructor adding the
    /// row up by hand to check gets the wrong answer.
    /// </summary>
    public decimal TotalGranted { get; init; }

    public static BonusLedger Empty { get; } = new() { ByColumnId = new Dictionary<Guid, decimal>() };

    public decimal ForColumn(Guid columnId) => ByColumnId.TryGetValue(columnId, out var v) ? v : 0m;

    /// <summary>
    /// Orders by (GrantedAt, Id) and then applies the <c>MaxBonusPerStudent</c> cap ONCE, cutting
    /// from the most recent grant backwards. Without the ordering, the cap would cut in whatever
    /// order the calling list happens to be in — and that order depends on the query, so the same
    /// set of bonus points read twice gives two different results.
    /// </summary>
    public static BonusLedger Build(IEnumerable<BonusEntry> bonuses, GradebookSettings settings)
    {
        var ordered = bonuses
            .Where(b => b.Points > 0m)                 // a bonus is always > 0; this guards against legacy data
            .OrderBy(b => b.GrantedAt).ThenBy(b => b.Id)
            .ToList();

        var remaining = settings.MaxBonusPerStudent;
        var byColumn = new Dictionary<Guid, decimal>();
        decimal toFinal = 0m;
        decimal total = 0m;

        foreach (var b in ordered)
        {
            var points = b.Points;

            if (remaining is { } cap)
            {
                if (cap <= 0m) break;                  // cap exhausted: every later grant has no effect
                points = Math.Min(points, cap);
                remaining = cap - points;
            }

            total += points;

            if (b.Target == BonusTarget.GradeColumn && b.ColumnId is { } columnId)
                byColumn[columnId] = byColumn.TryGetValue(columnId, out var existing) ? existing + points : points;
            else if (b.Target == BonusTarget.FinalTotal)
                toFinal += points;

            // Target = GradeColumn with a null ColumnId is corrupt data (a CHECK constraint already
            // blocks it at the DB). Skip it silently rather than throwing and breaking the
            // gradebook of the entire class.
        }

        return new BonusLedger { ByColumnId = byColumn, ToFinalTotal = toFinal, TotalGranted = total };
    }
}

/// <summary>
/// The whole score computation lives here, pure and free of I/O (ADR-006). The final score is NOT
/// persisted: weights and aggregation rules can change mid-term, and a persisted value would
/// silently drift out of step with them.
/// </summary>
public static class GradebookCalculator
{
    public static ValidationOutcome ValidateColumns(IReadOnlyList<GradeColumn> columns)
    {
        if (columns.Count == 0) return new(false, "Chưa có cột điểm nào.");

        var duplicate = columns.GroupBy(c => c.Key).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) return new(false, $"Khóa cột “{duplicate.Key}” bị trùng.");

        var totalWeight = columns.Sum(c => c.WeightPercent);

        return totalWeight == 100m
            ? new(true, null)
            : new(false, $"Tổng trọng số các cột phải bằng 100%, hiện tại là {totalWeight}%.");
    }

    /// <summary>Step 1 — a column's raw score: a hand edit wins, otherwise the sources are aggregated.</summary>
    public static decimal RawColumnScore(
        GradeColumn column, bool isManualOverride, decimal manualScore,
        IReadOnlyList<SourceScore> sources, GradebookSettings settings)
        => isManualOverride || column.Aggregation.Kind == AggregationKind.Manual
            ? Math.Clamp(manualScore, 0m, settings.MaxScore)
            : GradeAggregator.Apply(column.Aggregation, sources, settings.MaxScore);

    /// <summary>Step 3 — a column's score AFTER adding the bonus points attached to it, clamped to [0, MaxScore].</summary>
    public static decimal ColumnScore(
        GradeColumn column, decimal rawScore, BonusLedger ledger, GradebookSettings settings)
        => Math.Clamp(rawScore + ledger.ForColumn(column.Id), 0m, settings.MaxScore);

    /// <summary>Steps 4–6, for a class with no minimum-score condition set.</summary>
    public static decimal FinalScore(
        IReadOnlyList<GradeColumn> columns,
        IReadOnlyDictionary<Guid, decimal> rawColumnScores,
        BonusLedger ledger,
        GradebookSettings settings)
        => FinalScore(columns, rawColumnScores, ledger, [], settings);

    /// <summary>
    /// Steps 4–6. THE ORDER IS BUSINESS LOGIC, not an implementation detail: add the bonus into
    /// each column and clamp the column FIRST, then apply the weight, then add the final_total
    /// bonuses, and ONLY THEN apply the minimum-score conditions — a bonus point must never be
    /// able to lift a student past a gating threshold. And round exactly ONCE, at the very end:
    /// rounding each column and then summing drifts by as much as 0.2–0.3 points in a 6-column class.
    /// </summary>
    public static decimal FinalScore(
        IReadOnlyList<GradeColumn> columns,
        IReadOnlyDictionary<Guid, decimal> rawColumnScores,
        BonusLedger ledger,
        IReadOnlyList<GradeCondition> conditions,
        GradebookSettings settings)
    {
        if (columns.Count == 0) return 0m;

        var columnScores = new Dictionary<Guid, decimal>(columns.Count);
        decimal total = 0m;

        foreach (var c in columns)
        {
            var raw = rawColumnScores.TryGetValue(c.Id, out var v) ? v : 0m;   // ungraded = 0
            var score = ColumnScore(c, raw, ledger, settings);
            columnScores[c.Id] = score;
            total += score * c.WeightPercent / 100m;
        }

        // final_total bonuses go through no weight at all. A bonus pointing at a since-deleted
        // column sits in the ledger but no column ever reads it — it is skipped, and it does not
        // corrupt the rest of the gradebook.
        total += ledger.ToFinalTotal;

        total = GradeConditionEvaluator.Apply(total, columns, columnScores, conditions).Score;
        total = Math.Clamp(total, 0m, settings.MaxScore);

        // AwayFromZero, NOT .NET's default ToEven: 8.25 has to become 8.3.
        return Math.Round(total, settings.DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
