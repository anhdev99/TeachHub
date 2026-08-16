using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Badge conditions are declarative DATA, not code. Adding a new badge must mean adding a row to
/// a table, not shipping a deploy.
/// </summary>
public class BadgeEvaluatorTests
{
    private static BadgeCondition Cond(BadgeMetric metric, decimal threshold,
                                       BadgeComparison comparison = BadgeComparison.AtLeast) =>
        new() { Metric = metric, Threshold = threshold, Comparison = comparison };

    private static BadgeSpec Badge(string code, BadgeLogic logic, params BadgeCondition[] conditions) =>
        new(Guid.CreateVersion7(), code, logic, conditions);

    [Fact]
    public void A_badge_with_NO_conditions_is_NOT_awarded()
    {
        // Closed in the safe direction. An empty list satisfies "all conditions hold" under formal
        // logic, so the most natural way to write it (All(...)) would hand that badge to EVERY
        // student the moment an instructor saves a half-finished badge.
        var empty = Badge("chua_khai", BadgeLogic.All);

        BadgeEvaluator.IsEarned(empty, new LearnerMetrics { TotalXp = 999 }).ShouldBeFalse();
    }

    [Fact]
    public void Reaching_the_required_XP_earns_the_badge()
    {
        var badge = Badge("xp_100", BadgeLogic.All, Cond(BadgeMetric.TotalXp, 100));

        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { TotalXp = 100 }).ShouldBeTrue();
        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { TotalXp = 99 }).ShouldBeFalse();
    }

    [Fact]
    public void All_logic_requires_every_condition()
    {
        var badge = Badge("cham_chi", BadgeLogic.All,
            Cond(BadgeMetric.TotalXp, 100), Cond(BadgeMetric.CurrentStreakDays, 7));

        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { TotalXp = 500, CurrentStreakDays = 6 })
            .ShouldBeFalse();
        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { TotalXp = 500, CurrentStreakDays = 7 })
            .ShouldBeTrue();
    }

    [Fact]
    public void Any_logic_needs_only_one_condition_to_hold()
    {
        var badge = Badge("mot_trong_hai", BadgeLogic.Any,
            Cond(BadgeMetric.TotalXp, 1000), Cond(BadgeMetric.LongestStreakDays, 5));

        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { TotalXp = 10, LongestStreakDays = 5 })
            .ShouldBeTrue();
    }

    [Fact]
    public void A_seven_day_streak_earns_the_badge()
    {
        var badge = Badge("streak_7", BadgeLogic.All, Cond(BadgeMetric.CurrentStreakDays, 7));

        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { CurrentStreakDays = 7 }).ShouldBeTrue();
    }

    [Fact]
    public void Three_consecutive_on_time_submissions_earn_the_badge()
    {
        var badge = Badge("dung_han_3", BadgeLogic.All,
            Cond(BadgeMetric.ConsecutiveOnTimeSubmissions, 3));

        var metrics = new LearnerMetrics
        {
            ConsecutiveOnTimeSubmissions = BadgeEvaluator.CurrentOnTimeRun([true, true, true]),
        };

        BadgeEvaluator.IsEarned(badge, metrics).ShouldBeTrue();
    }

    [Fact]
    public void A_single_late_submission_breaks_the_on_time_run()
    {
        // The order is chronological submission order. "Consecutive" counts up to the MOST RECENT
        // submission, so one late entry in the middle has to cut the run — otherwise "consecutive"
        // just means "total".
        BadgeEvaluator.CurrentOnTimeRun([true, true, false, true]).ShouldBe(1);
        BadgeEvaluator.LongestOnTimeRun([true, true, false, true]).ShouldBe(2);
        BadgeEvaluator.CurrentOnTimeRun([]).ShouldBe(0);
        BadgeEvaluator.CurrentOnTimeRun([false]).ShouldBe(0);
    }

    [Fact]
    public void The_AtMost_comparison_works_for_a_never_missed_a_deadline_badge()
    {
        var badge = Badge("khong_tre_lan_nao", BadgeLogic.All,
            Cond(BadgeMetric.LateSubmissions, 0, BadgeComparison.AtMost),
            Cond(BadgeMetric.OnTimeSubmissions, 5));

        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { OnTimeSubmissions = 5, LateSubmissions = 0 })
            .ShouldBeTrue();
        BadgeEvaluator.IsEarned(badge, new LearnerMetrics { OnTimeSubmissions = 5, LateSubmissions = 1 })
            .ShouldBeFalse();
    }

    [Fact]
    public void An_already_held_badge_is_not_awarded_again()
    {
        var already = Badge("xp_100", BadgeLogic.All, Cond(BadgeMetric.TotalXp, 100));
        var fresh = Badge("xp_200", BadgeLogic.All, Cond(BadgeMetric.TotalXp, 200));

        var result = BadgeEvaluator.NewlyEarned(
            [already, fresh], new LearnerMetrics { TotalXp = 250 }, alreadyEarned: new HashSet<Guid> { already.Id });

        result.Count.ShouldBe(1);
        result[0].Code.ShouldBe("xp_200");
    }

    [Fact]
    public void Nothing_is_awarded_while_the_conditions_are_unmet()
    {
        var badge = Badge("xp_1000", BadgeLogic.All, Cond(BadgeMetric.TotalXp, 1000));

        BadgeEvaluator.NewlyEarned([badge], new LearnerMetrics { TotalXp = 999 }, new HashSet<Guid>())
            .ShouldBeEmpty();
    }
}
