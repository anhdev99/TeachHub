using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// The streak is the easiest thing in the whole motivation mechanism to get wrong, and the thing
/// students notice fastest: someone who studied five days straight and sees the number 3 stops
/// trusting everything else. This entire suite exists to answer exactly ONE question: which day
/// is "a day".
/// </summary>
public class StreakCalculatorTests
{
    private static readonly TimeZoneInfo Vn = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    /// <summary>A timestamp written in VIETNAM LOCAL TIME for readability.</summary>
    private static DateTimeOffset VnTime(int day, int hour, int minute = 0) =>
        new(2026, 8, day, hour, minute, 0, VnOffset);

    [Fact]
    public void Studying_at_23_50_then_00_10_the_next_day_counts_as_TWO_days()
    {
        // The classic case: two timestamps only 20 minutes apart but sitting on two different
        // LOCAL DAYS. A student who studied late and kept going past midnight has held a two-day
        // streak — returning 1 steals a day from them, and they can count.
        var lateNight = VnTime(16, 23, 50);
        var pastMidnight = VnTime(17, 0, 10);

        var result = StreakCalculator.Compute([lateNight, pastMidnight], Vn, now: pastMidnight);

        result.Current.ShouldBe(2);
        result.Longest.ShouldBe(2);
    }

    [Fact]
    public void The_very_same_two_timestamps_counted_in_UTC_give_only_ONE_day()
    {
        // 23:50 Vietnam time = 16:50Z, and 00:10 the next day Vietnam time = 17:10Z — the SAME
        // UTC day. This is exactly why we must convert into the class's time zone before
        // comparing: picking the wrong zone does not skew the result slightly, it erases an
        // entire day from the streak.
        var result = StreakCalculator.Compute(
            [VnTime(16, 23, 50), VnTime(17, 0, 10)], TimeZoneInfo.Utc, now: VnTime(17, 0, 10));

        result.Current.ShouldBe(1);
        result.Longest.ShouldBe(1);
    }

    [Fact]
    public void Three_consecutive_days_give_a_streak_of_three()
    {
        var result = StreakCalculator.Compute(
            [VnTime(14, 9), VnTime(15, 20), VnTime(16, 8)], Vn, now: VnTime(16, 22));

        result.Current.ShouldBe(3);
        result.Longest.ShouldBe(3);
    }

    [Fact]
    public void Missing_a_day_resets_the_streak_to_one()
    {
        // Studied on the 12th, 13th and 14th — took the 15th off — came back on the 16th.
        var result = StreakCalculator.Compute(
            [VnTime(12, 9), VnTime(13, 9), VnTime(14, 9), VnTime(16, 9)], Vn, now: VnTime(16, 21));

        result.Current.ShouldBe(1);

        // The old record is NOT wiped when the run breaks: a badge already awarded on the basis
        // of that record must not be allowed to vanish, or a student loses a badge over one sick day.
        result.Longest.ShouldBe(3);
    }

    [Fact]
    public void Two_activities_on_the_same_local_day_count_as_one_day()
    {
        // Studying twice as hard in one day does not buy an extra streak day.
        var result = StreakCalculator.Compute(
            [VnTime(16, 7, 30), VnTime(16, 23, 30)], Vn, now: VnTime(16, 23, 40));

        result.Current.ShouldBe(1);
        result.Longest.ShouldBe(1);
    }

    [Fact]
    public void Not_having_studied_today_but_having_studied_yesterday_keeps_the_run_alive()
    {
        // Without a grace period lasting until the end of the day, every student opening the page
        // at 7 in the morning sees streak = 0 and believes they just lost their run — after which
        // nobody trusts the number any more.
        var result = StreakCalculator.Compute(
            [VnTime(14, 9), VnTime(15, 9), VnTime(16, 9)], Vn, now: VnTime(17, 7));

        result.Current.ShouldBe(3);
    }

    [Fact]
    public void The_most_recent_activity_being_two_days_old_drops_the_streak_to_zero()
    {
        var result = StreakCalculator.Compute([VnTime(14, 9)], Vn, now: VnTime(16, 9));

        result.Current.ShouldBe(0);
        result.Longest.ShouldBe(1);
    }

    [Fact]
    public void With_no_activity_at_all_the_streak_is_zero()
    {
        var result = StreakCalculator.Compute([], Vn, now: VnTime(16, 9));

        result.Current.ShouldBe(0);
        result.Longest.ShouldBe(0);
        result.LastActiveLocalDate.ShouldBeNull();
    }

    [Fact]
    public void Timestamps_do_not_need_to_arrive_pre_sorted()
    {
        // What order the query returns rows in is the query's business; the streak result must
        // never depend on it.
        var result = StreakCalculator.Compute(
            [VnTime(16, 9), VnTime(14, 9), VnTime(15, 9)], Vn, now: VnTime(16, 10));

        result.Current.ShouldBe(3);
        result.LastActiveLocalDate.ShouldBe(new DateOnly(2026, 8, 16));
    }

    [Fact]
    public void An_invalid_time_zone_falls_back_to_Vietnam_time_instead_of_throwing()
    {
        // TimeZoneId is a free-form string in the database. Throwing here would let one corrupt
        // row take down the progress page for an entire class; falling back to the default is
        // off by a few hours at worst.
        StreakCalculator.ResolveZone("Khong/Ton_Tai").ShouldBe(Vn);
        StreakCalculator.ResolveZone(null).ShouldBe(Vn);
        StreakCalculator.ResolveZone("  ").ShouldBe(Vn);
        StreakCalculator.ResolveZone("Asia/Tokyo").Id.ShouldBe("Asia/Tokyo");
    }

    [Fact]
    public void The_local_date_shifts_with_the_time_zone_of_the_class()
    {
        var instant = new DateTimeOffset(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);   // 17:30Z

        StreakCalculator.LocalDateOf(instant, Vn).ShouldBe(new DateOnly(2026, 8, 17));       // 00:30 the next day
        StreakCalculator.LocalDateOf(instant, TimeZoneInfo.Utc).ShouldBe(new DateOnly(2026, 8, 16));
    }
}
