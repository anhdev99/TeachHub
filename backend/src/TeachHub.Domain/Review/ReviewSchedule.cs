namespace TeachHub.Domain.Review;

/// <summary>
/// A rung on the spaced-repetition ladder. The name of the rung IS the gap until the next review,
/// so reading one row in the DB tells you its schedule immediately — persisted as a STRING.
/// </summary>
public enum ReviewStage { ThreeDays, OneWeek, TwoWeeks, Mastered }

/// <summary>
/// The status of one item in the queue. Kept separate from <see cref="ReviewStage"/> because the
/// "Review today" query and the nightly scan job both filter on status, and a partial index
/// <c>WHERE status = 'Pending'</c> is far cheaper than filtering on the rung.
/// </summary>
public enum ReviewItemStatus { Pending, Mastered }

/// <param name="NextReviewAt">
/// null means there is NO further review (already mastered). Deliberately nullable rather than
/// leaving the old timestamp behind: an old timestamp left on a mastered item means that item sits
/// in "Review today" forever, and the student stops trusting the whole queue.
/// </param>
public readonly record struct ReviewScheduleResult(ReviewStage Stage, DateTimeOffset? NextReviewAt)
{
    public bool IsMastered => Stage == ReviewStage.Mastered;
}

/// <summary>
/// Every scheduling rule for spaced review — pure functions, no I/O, no reading the clock. The
/// current instant is always passed in through the <c>now</c> parameter (at the application tier
/// that is <c>TimeProvider</c>).
///
/// No function here may ever call <c>DateTimeOffset.UtcNow</c>: checking that an item walks the
/// full ladder using the real clock means waiting 24 days, so the rules would never get tested and
/// a bug on the last rung would go straight to production.
/// </summary>
public static class ReviewSchedule
{
    /// <summary>The fixed ladder of Phase 8: 3 days → 1 week → 2 weeks → mastered.</summary>
    public static readonly TimeSpan FirstInterval = TimeSpan.FromDays(3);

    /// <summary>The wait matching a rung; null on the mastered rung because there is no next review.</summary>
    public static TimeSpan? IntervalFor(ReviewStage stage) => stage switch
    {
        ReviewStage.ThreeDays => TimeSpan.FromDays(3),
        ReviewStage.OneWeek => TimeSpan.FromDays(7),
        ReviewStage.TwoWeeks => TimeSpan.FromDays(14),
        _ => null,
    };

    /// <summary>
    /// The schedule for an item that has just been created from a WRONG answer, and also the point
    /// the item returns to whenever it is answered wrongly on any rung.
    /// </summary>
    public static ReviewScheduleResult Start(DateTimeOffset now) =>
        new(ReviewStage.ThreeDays, now + FirstInterval);

    /// <summary>
    /// The next schedule after the student answers an item currently on rung <paramref name="stage"/>.
    ///
    /// The next timestamp is measured from <paramref name="now"/> — the moment of the ANSWER — and
    /// not from the old due date. Measuring from the old due date means a student who ignored the
    /// queue for ten days and only then reviewed gets forced to review again a few days later,
    /// i.e. the system piles pressure on exactly the person who is furthest behind.
    /// </summary>
    public static ReviewScheduleResult Next(ReviewStage stage, bool traLoiDung, DateTimeOffset now)
    {
        // A wrong answer goes back to the start of the cycle, NOT back one rung. Dropping one rung
        // sounds "fairer" but keeps the assumption that the student still remembers the part they
        // already reviewed — while the evidence just gathered says they do not. This applies to
        // mastered items too: a foundational concept missed again in a later course has to be
        // relearned from scratch (Gap #2 — the queue belongs to the PERSON, not to a class).
        if (!traLoiDung) return Start(now);

        var nextStage = stage switch
        {
            ReviewStage.ThreeDays => ReviewStage.OneWeek,
            ReviewStage.OneWeek => ReviewStage.TwoWeeks,
            _ => ReviewStage.Mastered,        // TwoWeeks answered right, and Mastered stays put
        };

        return new ReviewScheduleResult(
            nextStage,
            IntervalFor(nextStage) is { } interval ? now + interval : null);
    }
}
