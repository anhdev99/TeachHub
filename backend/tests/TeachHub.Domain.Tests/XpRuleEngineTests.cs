using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// The XP rule table is DATA, not code. Every number in this test suite is constructed by the
/// caller — the engine must not know any of them in advance.
/// </summary>
public class XpRuleEngineTests
{
    private static XpRuleSpec Rule(XpEventType eventType, int points, decimal? threshold = null,
                                   int? dailyCap = null, bool enabled = true) =>
        new() { EventType = eventType, Points = points, MinValue = threshold, DailyCap = dailyCap, IsEnabled = enabled };

    [Fact]
    public void An_event_with_no_matching_rule_earns_no_XP()
    {
        var result = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 9m),
                                           [Rule(XpEventType.LessonCompleted, 10)]);

        result.Points.ShouldBe(0);
        result.IsAwarded.ShouldBeFalse();
        result.Rule.ShouldBeNull();
    }

    [Fact]
    public void A_disabled_rule_is_not_applied()
    {
        // Turning a rule off has to take effect mid-semester without deleting it — deleting also
        // erases the history of why a student was ever awarded points.
        var result = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted),
                                           [Rule(XpEventType.LessonCompleted, 10, enabled: false)]);

        result.Points.ShouldBe(0);
    }

    [Fact]
    public void With_multiple_threshold_tiers_the_HIGHEST_tier_wins_instead_of_accumulating()
    {
        // A quiz scored 9.5 must earn the 30 XP of the ">= 9" tier, NOT 50 (= 20 + 30), and not
        // the 20 of the lower tier either. Accumulating is how a two-tier rule table silently
        // turns into a three-tier one that nobody declared.
        IReadOnlyList<XpRuleSpec> table =
        [
            Rule(XpEventType.QuizHighScore, 20, threshold: 8m),
            Rule(XpEventType.QuizHighScore, 30, threshold: 9m),
        ];

        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 9.5m), table).Points.ShouldBe(30);
        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 8.5m), table).Points.ShouldBe(20);
    }

    [Fact]
    public void A_score_exactly_at_the_threshold_still_counts()
    {
        // The threshold means "at or above". Using > instead of >= here leaves the student who
        // scored exactly 8.0 with no XP, and explaining that to them is impossible.
        var result = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 8m),
                                           [Rule(XpEventType.QuizHighScore, 20, threshold: 8m)]);

        result.Points.ShouldBe(20);
    }

    [Fact]
    public void Below_the_threshold_no_rule_applies()
    {
        var result = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 7.9m),
                                           [Rule(XpEventType.QuizHighScore, 20, threshold: 8m)]);

        result.Points.ShouldBe(0);
    }

    [Fact]
    public void An_event_carrying_no_value_does_not_trigger_a_threshold_rule()
    {
        // Letting a valueless event clear the threshold opens the door for any stray event to
        // collect top-tier XP.
        var result = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore),
                                           [Rule(XpEventType.QuizHighScore, 20, threshold: 8m)]);

        result.Points.ShouldBe(0);
    }

    [Fact]
    public void The_daily_cap_blocks_awards_beyond_it()
    {
        var table = new[] { Rule(XpEventType.LessonCompleted, 10, dailyCap: 3) };

        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 2), table)
            .Points.ShouldBe(10);

        var blocked = XpRuleEngine.Evaluate(
            new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 3), table);

        blocked.Points.ShouldBe(0);
        blocked.SkipReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void With_no_declared_cap_there_is_no_limit_on_awards_per_day()
    {
        var result = XpRuleEngine.Evaluate(
            new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 99),
            [Rule(XpEventType.LessonCompleted, 10)]);

        result.Points.ShouldBe(10);
    }

    [Fact]
    public void The_seed_rule_table_covers_the_three_events_from_the_requirements()
    {
        // SeedRules is only the SEED for a fresh class — still data, still editable afterwards.
        // It exists so that day one of a new class is not a blank table.
        var table = XpRuleEngine.SeedRules();

        table.ShouldContain(r => r.EventType == XpEventType.LessonCompleted);
        table.ShouldContain(r => r.EventType == XpEventType.AssignmentSubmittedOnTime);
        table.ShouldContain(r => r.EventType == XpEventType.QuizHighScore);
        table.ShouldAllBe(r => r.Points > 0);
    }

    [Fact]
    public void Two_rules_sharing_a_threshold_still_give_a_DETERMINISTIC_result()
    {
        // Duplicate data is a matter of time (the instructor clicks save twice). The outcome must
        // not depend on element order, otherwise the same student reads two different numbers on
        // two page loads.
        IReadOnlyList<XpRuleSpec> forward = [Rule(XpEventType.LessonCompleted, 5), Rule(XpEventType.LessonCompleted, 12)];
        IReadOnlyList<XpRuleSpec> reversed = [Rule(XpEventType.LessonCompleted, 12), Rule(XpEventType.LessonCompleted, 5)];

        var a = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted), forward).Points;
        var b = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted), reversed).Points;

        a.ShouldBe(b);
        a.ShouldBe(12);
    }
}
