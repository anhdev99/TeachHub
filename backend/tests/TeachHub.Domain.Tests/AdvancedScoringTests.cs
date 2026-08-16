using Shouldly;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Advanced grading (Phase 6b): late penalties, deductions for unlocking hints, and per-member
/// scores inside a group. This is the code that computes real student grades — every edge case
/// below was called out by name in the plan, because getting one wrong means getting a real
/// person's final grade wrong.
/// </summary>
public class AdvancedScoringTests
{
    private static readonly DateTimeOffset Due =
        new(2026, 9, 20, 23, 59, 0, TimeSpan.FromHours(7));

    // -- Days late -----------------------------------------------------------------------

    [Fact]
    public void Submitting_on_time_counts_zero_days_late()
    {
        LatePenaltyCalculator.DaysLate(Due, Due).ShouldBe(0);
        LatePenaltyCalculator.DaysLate(Due, Due.AddHours(-3)).ShouldBe(0);
    }

    [Fact]
    public void Being_exactly_1_minute_late_counts_as_1_day()
    {
        // Round UP — late is late. Rounding down means the whole class submits at 00:05 and
        // nobody is penalized at all, which is to say the deadline stops meaning anything.
        LatePenaltyCalculator.DaysLate(Due, Due.AddMinutes(1)).ShouldBe(1);
        LatePenaltyCalculator.DaysLate(Due, Due.AddSeconds(1)).ShouldBe(1);
    }

    [Fact]
    public void Being_exactly_24_hours_late_is_1_day_not_2()
    {
        // This case breaks if computed as Math.Ceiling(TotalDays): TotalDays is a double, and a
        // clean 24 hours can come out as 1.0000000000000002 depending on the subtraction — at
        // which point a student submitting exactly 24 hours late is penalized twice as hard as
        // one submitting a second after that. It has to be computed in integer ticks.
        LatePenaltyCalculator.DaysLate(Due, Due.AddDays(1)).ShouldBe(1);
        LatePenaltyCalculator.DaysLate(Due, Due.AddDays(1).AddSeconds(1)).ShouldBe(2);
        LatePenaltyCalculator.DaysLate(Due, Due.AddDays(3)).ShouldBe(3);
    }

    [Fact]
    public void With_no_deadline_nothing_is_ever_late()
    {
        LatePenaltyCalculator.DaysLate(null, Due.AddDays(10)).ShouldBe(0);
        LatePenaltyCalculator.DaysLate(Due, null).ShouldBe(0);
    }

    [Fact]
    public void Days_late_does_not_depend_on_the_offset_the_timestamp_was_written_in()
    {
        // The same instant written with two different offsets must give the same answer.
        // Otherwise a 23:59 Vietnam-time deadline is interpreted differently depending on where
        // the server happens to sit.
        var submittedVn = Due.AddHours(25);
        var submittedUtc = submittedVn.ToUniversalTime();

        LatePenaltyCalculator.DaysLate(Due, submittedUtc)
            .ShouldBe(LatePenaltyCalculator.DaysLate(Due, submittedVn));
    }

    // -- Late penalty --------------------------------------------------------------------

    [Fact]
    public void The_late_penalty_deducts_the_declared_percentage_per_day()
    {
        var result = LatePenaltyCalculator.Apply(10m, 10m, Due, Due.AddDays(2));

        result.DaysLate.ShouldBe(2);
        result.PercentDeducted.ShouldBe(20m);
        result.FinalScore.ShouldBe(8m);
        result.PenaltyPoints.ShouldBe(2m);
    }

    [Fact]
    public void The_late_penalty_NEVER_pushes_the_score_negative()
    {
        // 30%/day x 5 days = 150%. Without clamping the score comes out at -5, and a negative
        // column drags the student's final grade below even the assignments they never handed in.
        var result = LatePenaltyCalculator.Apply(10m, 30m, Due, Due.AddDays(5));

        result.FinalScore.ShouldBe(0m);
        result.PercentDeducted.ShouldBe(100m);      // report what was ACTUALLY deducted, not 150%
        result.PenaltyPoints.ShouldBe(10m);
    }

    [Fact]
    public void A_late_penalty_of_0_percent_deducts_nothing()
    {
        LatePenaltyCalculator.Apply(9.5m, 0m, Due, Due.AddDays(4)).FinalScore.ShouldBe(9.5m);
    }

    [Fact]
    public void Submitting_on_time_leaves_the_score_untouched()
    {
        var result = LatePenaltyCalculator.Apply(7.25m, 50m, Due, Due.AddMinutes(-1));

        result.DaysLate.ShouldBe(0);
        result.FinalScore.ShouldBe(7.25m);
    }

    [Fact]
    public void The_post_penalty_score_is_rounded_to_2_decimal_places()
    {
        // 8.33 x 0.85 = 7.0805 -> 7.08. Without rounding, the decimal(5,2) column truncates on
        // its own and the displayed number drifts from the number used for the final grade —
        // the student adds it up and finds it wrong.
        LatePenaltyCalculator.Apply(8.33m, 15m, Due, Due.AddHours(1)).FinalScore.ShouldBe(7.08m);
    }

    // -- Hint unlock penalty --------------------------------------------------------------

    [Fact]
    public void Unlocking_no_hints_leaves_the_score_untouched()
    {
        var result = HintPenaltyCalculator.Apply(9m, []);

        result.HintsUnlocked.ShouldBe(0);
        result.PercentDeducted.ShouldBe(0m);
        result.FinalScore.ShouldBe(9m);
    }

    [Fact]
    public void Unlocking_several_hints_accumulates_the_deduction_percentage()
    {
        var result = HintPenaltyCalculator.Apply(10m, [10m, 15m]);

        result.HintsUnlocked.ShouldBe(2);
        result.PercentDeducted.ShouldBe(25m);
        result.FinalScore.ShouldBe(7.5m);
    }

    [Fact]
    public void A_free_hint_costs_nothing()
    {
        HintPenaltyCalculator.Apply(10m, [0m, 0m]).FinalScore.ShouldBe(10m);
    }

    [Fact]
    public void Deductions_totalling_over_100_percent_floor_the_score_at_0_rather_than_going_negative()
    {
        var result = HintPenaltyCalculator.Apply(10m, [60m, 60m]);

        result.PercentDeducted.ShouldBe(100m);
        result.FinalScore.ShouldBe(0m);
    }

    // -- Both penalties combined ----------------------------------------------------------

    [Fact]
    public void The_hint_penalty_and_the_late_penalty_compound_on_the_raw_score()
    {
        // 10 x (1 - 0.20) x (1 - 0.10) = 7.2
        var result = SubmissionScoring.Apply(10m, 10m, Due, Due.AddDays(1), [20m]);

        result.RawScore.ShouldBe(10m);
        result.DaysLate.ShouldBe(1);
        result.LatePenaltyPercent.ShouldBe(10m);
        result.HintsUnlocked.ShouldBe(1);
        result.HintPenaltyPercent.ShouldBe(20m);
        result.FinalScore.ShouldBe(7.2m);
    }

    [Fact]
    public void Combining_both_penalties_still_never_produces_a_negative_score()
    {
        SubmissionScoring.Apply(10m, 80m, Due, Due.AddDays(3), [90m]).FinalScore.ShouldBe(0m);
    }

    [Fact]
    public void Applying_it_repeatedly_to_the_same_raw_score_gives_the_same_result()
    {
        // Computed from the RAW score, never stacked on an already-penalized one. Stacking means
        // every time the instructor clicks "recalculate" the student's grade silently drops
        // another notch.
        var first = SubmissionScoring.Apply(10m, 10m, Due, Due.AddDays(1), [20m]);
        var second = SubmissionScoring.Apply(first.RawScore, 10m, Due, Due.AddDays(1), [20m]);

        second.FinalScore.ShouldBe(first.FinalScore);
    }

    [Fact]
    public void With_no_lateness_and_no_hints_the_score_is_preserved_exactly()
    {
        SubmissionScoring.Apply(8.75m, 25m, Due, Due.AddHours(-1), []).FinalScore.ShouldBe(8.75m);
    }

    // -- Per-member scores inside a group (UC-10b) -----------------------------------------

    private static readonly Guid An = Guid.CreateVersion7();
    private static readonly Guid Binh = Guid.CreateVersion7();
    private static readonly Guid Cuong = Guid.CreateVersion7();

    private static SubmissionMemberScore MemberScore(Guid enrollmentId, decimal score) => new()
    {
        SubmissionId = Guid.CreateVersion7(),
        EnrollmentId = enrollmentId,
        Score = score,
        Reason = "Không tham gia phần lập trình.",
    };

    [Fact]
    public void With_no_per_member_score_the_whole_group_receives_the_shared_score()
    {
        var result = MemberScoring.Resolve(8m, [An, Binh, Cuong], []);

        result.Count.ShouldBe(3);
        result.ShouldAllBe(m => m.Score == 8m && !m.IsOverridden);
    }

    [Fact]
    public void A_per_member_score_OVERRIDES_the_shared_score()
    {
        var result = MemberScoring.Resolve(8m, [An, Binh, Cuong], [MemberScore(Binh, 5m)]);

        result.Single(m => m.EnrollmentId == Binh).Score.ShouldBe(5m);
        result.Single(m => m.EnrollmentId == Binh).IsOverridden.ShouldBeTrue();
        result.Single(m => m.EnrollmentId == Binh).Reason.ShouldNotBeNullOrWhiteSpace();

        result.Single(m => m.EnrollmentId == An).Score.ShouldBe(8m);
        result.Single(m => m.EnrollmentId == An).IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void A_per_member_score_higher_than_the_shared_one_also_overrides()
    {
        // The override works in both directions: some groups have one person carrying the work.
        MemberScoring.Resolve(6m, [An, Binh], [MemberScore(An, 9m)])
            .Single(m => m.EnrollmentId == An).Score.ShouldBe(9m);
    }

    [Fact]
    public void A_per_member_score_on_an_ungraded_submission_still_takes_effect()
    {
        var result = MemberScoring.Resolve(null, [An, Binh], [MemberScore(An, 4m)]);

        result.Single(m => m.EnrollmentId == An).Score.ShouldBe(4m);
        result.Single(m => m.EnrollmentId == Binh).Score.ShouldBeNull();
    }

    [Fact]
    public void A_per_member_score_for_someone_outside_the_group_is_ignored()
    {
        // Leftover junk data after the instructor moves a student to a different group. Accepting
        // it blindly makes the grade sheet show a row for someone who is not in this group.
        var result = MemberScoring.Resolve(8m, [An, Binh], [MemberScore(Cuong, 2m)]);

        result.Count.ShouldBe(2);
        result.ShouldNotContain(m => m.EnrollmentId == Cuong);
    }

    [Fact]
    public void An_empty_group_does_not_blow_up()
    {
        MemberScoring.Resolve(8m, [], []).ShouldBeEmpty();
    }
}
