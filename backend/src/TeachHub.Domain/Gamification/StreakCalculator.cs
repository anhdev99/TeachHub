namespace TeachHub.Domain.Gamification;

/// <param name="Current">
/// The number of consecutive days up to TODAY (in the class's time zone). Still "alive" if the
/// most recent activity was today or yesterday — see the grace-period note in
/// <see cref="StreakCalculator.Compute"/>.
/// </param>
/// <param name="Longest">
/// The longest run across the entire history. NOT cleared when the current run breaks: a badge
/// already awarded on the strength of that record must not vanish just because the student took
/// a day off sick.
/// </param>
public sealed record StreakOutcome(int Current, int Longest, DateOnly? LastActiveLocalDate)
{
    public static StreakOutcome None { get; } = new(0, 0, null);
}

/// <summary>
/// The entire definition of a "day" for the streak mechanism lives here, purely, with no I/O.
///
/// THE CORE DECISION: a "day" is a LOCAL CALENDAR DAY in the class's time zone
/// (<c>CourseOffering.TimeZoneId</c>, defaulting to <c>Asia/Ho_Chi_Minh</c>), NOT a UTC day and
/// not "24 hours since the last time" either. The concrete consequence, locked down by tests:
/// studying at 23:50 and then at 00:10 the next day is TWO days (exactly how the student counts
/// it), whereas counting in UTC gives ONE because both instants land on 16:50Z and 17:10Z of the
/// same day. Picking the wrong time zone does not shift things slightly — it erases a whole day
/// from the run, and students notice immediately.
/// </summary>
public static class StreakCalculator
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// <c>TimeZoneId</c> is a free-form string in the DB, so it can be wrong (a typo, an import, a
    /// zone renamed between ICU releases). Throwing here would let ONE bad row take down the
    /// progress page for the whole class; falling back to Vietnam time is off by a few hours at
    /// worst for a class in another zone.
    /// </summary>
    public static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Fall through to the default below.
            }
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // The server has no time zone database (InvariantGlobalization switched on somewhere
            // by mistake). UTC still gives a usable result instead of breaking the whole feature.
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>The local calendar date of an absolute instant.</summary>
    public static DateOnly LocalDateOf(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    /// <summary>
    /// <paramref name="activityMoments"/> does not have to be sorted beforehand: the order is
    /// whatever the query decides, and the streak result must never depend on it.
    ///
    /// GRACE UNTIL THE END OF THE DAY: if there is no activity today but there was one yesterday,
    /// the run still counts as alive. Without this grace period every student who opens the page
    /// at 7 in the morning sees streak = 0, believes they have just lost their run, and nobody
    /// trusts that number again.
    /// </summary>
    public static StreakOutcome Compute(
        IEnumerable<DateTimeOffset> activityMoments, TimeZoneInfo zone, DateTimeOffset now)
    {
        // Collapse to LOCAL DAYS before comparing anything. Several activities within the same day
        // are worth exactly one streak day — studying twice as hard does not buy an extra day.
        var days = new HashSet<DateOnly>();
        foreach (var moment in activityMoments) days.Add(LocalDateOf(moment, zone));

        if (days.Count == 0) return StreakOutcome.None;

        var ordered = days.Order().ToList();
        var today = LocalDateOf(now, zone);

        // ── Longest record: scan the entire history ───────────────────────────────────────
        var longest = 1;
        var run = 1;

        for (var i = 1; i < ordered.Count; i++)
        {
            run = ordered[i] == ordered[i - 1].AddDays(1) ? run + 1 : 1;
            if (run > longest) longest = run;
        }

        // ── Current run: count backwards from the anchor ──────────────────────────────────
        DateOnly? anchor = days.Contains(today) ? today
                         : days.Contains(today.AddDays(-1)) ? today.AddDays(-1)
                         : null;

        var current = 0;

        if (anchor is { } anchorDay)
        {
            var d = anchorDay;
            while (days.Contains(d))
            {
                current++;
                d = d.AddDays(-1);
            }
        }

        return new StreakOutcome(current, longest, ordered[^1]);
    }
}
