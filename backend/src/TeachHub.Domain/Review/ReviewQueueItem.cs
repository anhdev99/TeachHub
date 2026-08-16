using TeachHub.Domain.Common;

namespace TeachHub.Domain.Review;

/// <summary>
/// One concept a student answered wrongly and needs to review again.
///
/// Gap #2 — attached to <see cref="UserId"/> and NOT to enrollment_id, unlike every other entity
/// in the project. Foundational knowledge belongs to a person, not to a course offering: a student
/// who got "pointers" wrong in Programming in C and then moves on to Data Structures still has to
/// review that exact concept. Attaching it to the enrollment wipes the queue clean at the end of
/// every term, and the entire value of spaced review lies in it carrying across terms.
/// Do not "fix this for consistency" with the other entities.
/// </summary>
public class ReviewQueueItem : Entity, IAuditable
{
    public required Guid UserId { get; set; }
    public required Guid QuestionId { get; set; }

    /// <summary>
    /// The quiz attempt that produced this item — kept purely for tracing, so we can explain to a
    /// student why this question is in the queue. Nullable, and NEVER repointed at the old attempt
    /// when the item is refilled: an item outlives the attempt that created it.
    /// </summary>
    public Guid? SourceQuizAttemptId { get; set; }

    public ReviewStage Stage { get; set; } = ReviewStage.ThreeDays;

    /// <summary>
    /// Always travels together with <see cref="Stage"/> and may ONLY be changed through
    /// <see cref="ApplyAnswer"/>. Assigning these two fields by hand in two different places is the
    /// surest way to make them disagree, and an item with Stage=Mastered but Status=Pending will
    /// sit in "Review today" forever with NextReviewAt null — never due, and never going away.
    /// </summary>
    public ReviewItemStatus Status { get; set; } = ReviewItemStatus.Pending;

    /// <summary>null = mastered, no further review.</summary>
    public DateTimeOffset? NextReviewAt { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// A new item is born from ONE wrong answer, so <see cref="WrongCount"/> starts at 1 and not
    /// at 0 — starting at 0 throws away the very event that created the item, and later on there
    /// is no way to explain why an item that was never answered wrongly is sitting in the queue.
    /// </summary>
    public static ReviewQueueItem Create(
        Guid userId, Guid questionId, Guid? sourceQuizAttemptId, DateTimeOffset now)
    {
        var schedule = ReviewSchedule.Start(now);

        return new ReviewQueueItem
        {
            UserId = userId,
            QuestionId = questionId,
            SourceQuizAttemptId = sourceQuizAttemptId,
            Stage = schedule.Stage,
            Status = ReviewItemStatus.Pending,
            NextReviewAt = schedule.NextReviewAt,
            WrongCount = 1,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Records one answer and moves the schedule. This is the ONLY path that changes
    /// <see cref="Stage"/>/<see cref="Status"/>/<see cref="NextReviewAt"/> — those three fields
    /// must change together, always.
    /// </summary>
    public void ApplyAnswer(bool dung, DateTimeOffset now)
    {
        var schedule = ReviewSchedule.Next(Stage, dung, now);

        Stage = schedule.Stage;
        Status = schedule.IsMastered ? ReviewItemStatus.Mastered : ReviewItemStatus.Pending;
        NextReviewAt = schedule.NextReviewAt;
        LastReviewedAt = now;
        UpdatedAt = now;

        if (dung) CorrectCount++;
        else WrongCount++;
    }

    /// <summary>
    /// Whether the item is due for review. This condition is repeated verbatim in the "Review
    /// today" query and in the table's partial index — change it here and you must change both of
    /// those too.
    /// </summary>
    public bool IsDue(DateTimeOffset now) =>
        Status == ReviewItemStatus.Pending && NextReviewAt is { } due && due <= now;
}

/// <summary>
/// A marker that one quiz attempt HAS already been ingested into the review queue.
///
/// Without this table the nightly scan job cannot tell "this attempt has not been ingested yet"
/// apart from "this attempt was ingested, but all 3 wrong questions already had items in the
/// queue" — so it would re-ingest that attempt every night, each pass counting as a WRONG answer,
/// which pushes the student's rung back to 3 days forever. The review schedule never advances, no
/// exception is thrown, no test goes red.
/// </summary>
public class ReviewGenerationMark : Entity
{
    public required Guid QuizAttemptId { get; set; }
    public required DateTimeOffset GeneratedAt { get; set; }

    /// <summary>How many items were created and how many existing ones were pushed back to the start of the cycle — for reading logs only.</summary>
    public int ItemsCreated { get; set; }
    public int ItemsReset { get; set; }
}
