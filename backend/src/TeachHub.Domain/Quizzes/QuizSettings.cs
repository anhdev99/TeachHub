using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

/// <summary>
/// Links a question from the bank into one specific quiz. A dedicated join table rather than a
/// copy of the question: fixing one typo in the bank has to be fixed in exactly one place.
/// </summary>
public class QuizItem : Entity
{
    public required Guid QuizId { get; set; }
    public required Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>Overrides the question's points for this quiz only. Null = use the question's own points.</summary>
    public decimal? PointsOverride { get; set; }
}

/// <summary>
/// Configuration of one quiz. Stored directly on <c>Quiz</c> (no separate table): every field
/// only means anything together with that quiz and is never queried on its own.
/// </summary>
public class QuizConfig
{
    /// <summary>
    /// false = checkpoint quiz: not graded, shows the explanation right after the answer.
    /// true = graded quiz: has a time limit, an attempt limit, and the score flows into the gradebook.
    /// </summary>
    public bool IsGraded { get; set; }

    /// <summary>null = no time limit.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>null = as many attempts as the student wants.</summary>
    public int? MaxAttempts { get; set; }

    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }

    /// <summary>Show the correct answer + explanation right after submitting. On by default for checkpoint quizzes.</summary>
    public bool ShowExplanationAfterSubmit { get; set; } = true;

    public QuizConfig DeepCopy() => new()
    {
        IsGraded = IsGraded,
        TimeLimitMinutes = TimeLimitMinutes,
        MaxAttempts = MaxAttempts,
        ShuffleQuestions = ShuffleQuestions,
        ShuffleOptions = ShuffleOptions,
        ShowExplanationAfterSubmit = ShowExplanationAfterSubmit,
    };
}

/// <summary>
/// Timing rules for one attempt — pure functions, so the "time is up" case is testable without
/// waiting for real time to pass.
/// </summary>
public static class QuizTiming
{
    /// <summary>
    /// A 30-second grace period: slow network, the user hitting submit on the very last second, or
    /// a few seconds of drift between the server clock and the browser clock. Without the grace
    /// period a legitimate submission gets treated as being out of time.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);

    public static DateTimeOffset? HanNop(DateTimeOffset startedAt, int? timeLimitMinutes) =>
        timeLimitMinutes is { } minutes ? startedAt.AddMinutes(minutes) : null;

    public static bool DaHetGio(DateTimeOffset startedAt, int? timeLimitMinutes, DateTimeOffset now) =>
        HanNop(startedAt, timeLimitMinutes) is { } deadline && now > deadline + GracePeriod;

    public static TimeSpan? ConLai(DateTimeOffset startedAt, int? timeLimitMinutes, DateTimeOffset now)
    {
        if (HanNop(startedAt, timeLimitMinutes) is not { } deadline) return null;
        var remaining = deadline - now;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}
