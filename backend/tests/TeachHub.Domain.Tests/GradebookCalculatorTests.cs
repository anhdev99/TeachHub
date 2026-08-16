using Shouldly;
using TeachHub.Domain.Grading;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Kept even though the phase ran under a "no tests" instruction. This is the path travelled by
/// the number submitted to the academic office: one miscomputed weight, or a bonus cap applied
/// twice, has no symptom in the UI — it only surfaces once the grade sheet has been filed.
/// The whole file runs in under 50 ms.
/// </summary>
public class GradebookCalculatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

    private static GradeColumn Column(string key, decimal weight, AggregationKind kind = AggregationKind.Average)
        => new()
        {
            OfferingId = Guid.CreateVersion7(), Key = key, Name = key,
            WeightPercent = weight, Aggregation = new GradeAggregation { Kind = kind },
        };

    // -- Normalizing to a 10-point scale ---------------------------------------------------

    [Fact]
    public void A_source_on_a_20_point_scale_is_normalized_to_10_before_aggregating()
    {
        // A rubric totalling 20 on which the student scored 16 -> the column gets 8.0,
        // NOT 16 subsequently clamped to 10.
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average },
            [new SourceScore(16m, 20m)]);

        score.ShouldBe(8m);
    }

    [Fact]
    public void With_no_sources_it_returns_0_rather_than_dividing_by_0()
    {
        GradeAggregator.Apply(new GradeAggregation { Kind = AggregationKind.Average }, [])
            .ShouldBe(0m);
    }

    [Fact]
    public void An_ungraded_submission_counts_as_0_when_TreatMissingAsZero()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average, TreatMissingAsZero = true },
            [new SourceScore(10m, 10m), new SourceScore(null, 10m)]);

        score.ShouldBe(5m);
    }

    [Fact]
    public void An_ungraded_submission_leaves_the_denominator_when_TreatMissingAsZero_is_false()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average, TreatMissingAsZero = false },
            [new SourceScore(10m, 10m), new SourceScore(null, 10m)]);

        score.ShouldBe(10m);
    }

    // -- Aggregation rules -----------------------------------------------------------------

    [Fact]
    public void Drop_the_n_lowest()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.DropLowestN, N = 1 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m), new SourceScore(9m, 10m)]);

        score.ShouldBe(8.5m);
    }

    [Fact]
    public void Dropping_the_n_lowest_never_drops_everything_and_divides_by_0()
    {
        // N = 5 across 2 sources: at least one submission has to survive.
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.DropLowestN, N = 5 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m)]);

        score.ShouldBe(8m);
    }

    [Fact]
    public void Take_the_n_best()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.BestNOfM, N = 2 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m), new SourceScore(9m, 10m)]);

        score.ShouldBe(8.5m);
    }

    [Fact]
    public void Summing_is_clamped_at_the_top_of_the_scale()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Sum },
            [new SourceScore(7m, 10m), new SourceScore(8m, 10m)]);

        score.ShouldBe(10m);
    }

    [Fact]
    public void Weighted_average()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.WeightedAverage },
            [new SourceScore(10m, 10m, 3m), new SourceScore(6m, 10m, 1m)]);

        score.ShouldBe(9m);
    }

    [Fact]
    public void With_every_weight_left_blank_it_falls_back_to_an_even_split()
    {
        var score = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.WeightedAverage },
            [new SourceScore(10m, 10m, 0m), new SourceScore(6m, 10m, 0m)]);

        score.ShouldBe(8m);
    }

    // -- The bonus ledger: deterministic, with the cap applied once ------------------------

    [Fact]
    public void The_bonus_cap_is_applied_ONCE_across_the_whole_ledger()
    {
        var column = Guid.CreateVersion7();

        var ledger = BonusLedger.Build(
            [
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.GradeColumn, column, 0.5m, T0),
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.5m, T0.AddHours(1)),
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.5m, T0.AddHours(2)),
            ],
            GradebookSettings.Default with { MaxBonusPerStudent = 1m });

        ledger.TotalGranted.ShouldBe(1m);
        ledger.ForColumn(column).ShouldBe(0.5m);
        ledger.ToFinalTotal.ShouldBe(0.5m);   // the third grant already exhausted the cap
    }

    [Fact]
    public void Input_order_does_NOT_change_how_the_cap_is_applied()
    {
        // Blocker B2: without a deterministic sort, the cap truncates in whatever order the query
        // returned rows, and reading the same set of bonus entries twice gives two answers.
        var a = new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.7m, T0);
        var b = new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.7m, T0.AddHours(1));
        var settings = GradebookSettings.Default with { MaxBonusPerStudent = 1m };

        BonusLedger.Build([a, b], settings).ToFinalTotal
            .ShouldBe(BonusLedger.Build([b, a], settings).ToFinalTotal);
    }

    [Fact]
    public void A_negative_bonus_is_ignored()
    {
        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, -5m, T0)],
            GradebookSettings.Default);

        ledger.ToFinalTotal.ShouldBe(0m);
    }

    // -- The final score --------------------------------------------------------------------

    [Fact]
    public void The_final_score_follows_the_column_weights()
    {
        var coursework = Column("thuong_ky", 40m);
        var final = Column("cuoi_mon", 60m);

        var score = GradebookCalculator.FinalScore(
            [coursework, final],
            new Dictionary<Guid, decimal> { [coursework.Id] = 8m, [final.Id] = 6m },
            BonusLedger.Empty, GradebookSettings.Default);

        score.ShouldBe(6.8m);   // 8x0.4 + 6x0.6
    }

    [Fact]
    public void An_ungraded_column_counts_as_0()
    {
        var coursework = Column("thuong_ky", 40m);
        var final = Column("cuoi_mon", 60m);

        GradebookCalculator.FinalScore(
            [coursework, final],
            new Dictionary<Guid, decimal> { [coursework.Id] = 10m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(4m);
    }

    [Fact]
    public void A_bonus_added_to_a_column_is_clamped_at_10_before_the_weight_is_applied()
    {
        var coursework = Column("thuong_ky", 100m);

        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.GradeColumn, coursework.Id, 2m, T0)],
            GradebookSettings.Default);

        GradebookCalculator.FinalScore(
            [coursework], new Dictionary<Guid, decimal> { [coursework.Id] = 9.5m },
            ledger, GradebookSettings.Default)
            .ShouldBe(10m);
    }

    [Fact]
    public void Rounding_happens_exactly_ONCE_at_the_final_step()
    {
        // Rounding each column and then summing drifts by 0.2-0.3 points across a 6-column class.
        var a = Column("a", 50m);
        var b = Column("b", 50m);

        GradebookCalculator.FinalScore(
            [a, b],
            new Dictionary<Guid, decimal> { [a.Id] = 8.44m, [b.Id] = 8.46m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(8.5m);
    }

    [Fact]
    public void Rounding_is_AwayFromZero_not_to_even()
    {
        var a = Column("a", 100m);

        GradebookCalculator.FinalScore(
            [a], new Dictionary<Guid, decimal> { [a.Id] = 8.25m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(8.3m);
    }

    // -- Minimum-score conditions ------------------------------------------------------------

    [Fact]
    public void A_final_exam_below_the_floor_caps_the_overall_score()
    {
        // A common regulation: a final exam under 4.0 fails the subject no matter how good the
        // coursework was.
        var coursework = Column("thuong_ky", 40m);
        var final = Column("cuoi_mon", 60m);

        var score = GradebookCalculator.FinalScore(
            [coursework, final],
            new Dictionary<Guid, decimal> { [coursework.Id] = 9m, [final.Id] = 3.5m },
            BonusLedger.Empty,
            [new GradeCondition { ColumnKey = "cuoi_mon", Threshold = 4m, ActionValue = 3.9m }],
            GradebookSettings.Default);

        score.ShouldBe(3.9m);
    }

    [Fact]
    public void A_bonus_on_the_overall_score_does_NOT_lift_it_past_the_cap()
    {
        // Step 5 comes after step 4 precisely to block this: class participation points must not
        // be allowed to pass a student who scored 3.5 on the exam.
        var coursework = Column("thuong_ky", 40m);
        var final = Column("cuoi_mon", 60m);

        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 2m, T0)],
            GradebookSettings.Default);

        GradebookCalculator.FinalScore(
            [coursework, final],
            new Dictionary<Guid, decimal> { [coursework.Id] = 9m, [final.Id] = 3.5m },
            ledger,
            [new GradeCondition { ColumnKey = "cuoi_mon", Threshold = 4m, ActionValue = 3.9m }],
            GradebookSettings.Default)
            .ShouldBe(3.9m);
    }

    [Fact]
    public void When_several_conditions_hold_the_LOWEST_cap_wins()
    {
        var a = Column("a", 50m);
        var b = Column("b", 50m);

        GradebookCalculator.FinalScore(
            [a, b],
            new Dictionary<Guid, decimal> { [a.Id] = 2m, [b.Id] = 2m },
            BonusLedger.Empty,
            [
                new GradeCondition { ColumnKey = "a", Threshold = 4m, ActionValue = 3.9m },
                new GradeCondition { ColumnKey = "b", Threshold = 4m, ActionValue = 3m },
            ],
            GradebookSettings.Default)
            .ShouldBe(2m);   // already at 2.0, and both caps sit higher, so nothing is truncated
    }

    [Fact]
    public void A_condition_pointing_at_a_nonexistent_column_is_ignored_but_reported()
    {
        var a = Column("a", 100m);

        var result = GradeConditionEvaluator.Apply(
            8m, [a], new Dictionary<Guid, decimal> { [a.Id] = 8m },
            [new GradeCondition { ColumnKey = "khong_co", Threshold = 4m, ActionValue = 3.9m }]);

        result.Score.ShouldBe(8m);
        result.UnknownColumnKeys.ShouldBe(["khong_co"]);
    }

    // -- Column configuration checks ---------------------------------------------------------

    [Fact]
    public void Weights_not_totalling_100_are_rejected()
    {
        var result = GradebookCalculator.ValidateColumns([Column("a", 40m), Column("b", 40m)]);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("100%");
    }

    [Fact]
    public void Duplicate_keys_are_rejected()
    {
        GradebookCalculator.ValidateColumns([Column("a", 50m), Column("a", 50m)])
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_valid_set_of_columns_passes()
    {
        GradebookCalculator.ValidateColumns([Column("thuong_ky", 40m), Column("cuoi_mon", 60m)])
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_manual_override_beats_the_aggregation_rule()
    {
        var c = Column("thuong_ky", 100m);

        GradebookCalculator.RawColumnScore(
            c, isManualOverride: true, manualScore: 7m,
            [new SourceScore(2m, 10m)], GradebookSettings.Default)
            .ShouldBe(7m);
    }
}
