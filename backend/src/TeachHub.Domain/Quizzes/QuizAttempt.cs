using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

public enum QuizAttemptStatus { InProgress, Submitted, AutoSubmitted }

public class QuizAttempt : Entity
{
    public required Guid QuizId { get; set; }

    /// <summary>Gap #2: points at the enrollment, not at the user — retaking the course next term is a different attempt history.</summary>
    public required Guid EnrollmentId { get; set; }

    public int AttemptNo { get; set; } = 1;

    /// <summary>
    /// The server is the source of truth about time. A client clock can be changed with one line
    /// of JavaScript, so the deadline is always this instant plus the quiz's time_limit.
    /// </summary>
    public required DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
    public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;

    /// <summary>
    /// Saved answers, updated after EVERY question instead of waiting for the final submit — stored as jsonb.
    /// A student who loses wifi mid-quiz and loses everything will demand a retake, and there is no way to verify the claim.
    /// </summary>
    public List<StoredAnswer> Answers { get; set; } = [];

    public decimal? RawScore { get; set; }
    public decimal? MaxScore { get; set; }

    /// <summary>Score normalized onto the 0–10 scale — the number that flows into the gradebook.</summary>
    public decimal? NormalizedScore { get; set; }

    /// <summary>Source of the spaced review queue (Phase 8).</summary>
    public List<Guid> WrongQuestionIds { get; set; } = [];

    /// <summary>
    /// The shuffled question order for this attempt alone. Persisted so that reloading the page
    /// does not reorder anything — reshuffling on every open makes the student think the paper
    /// was swapped out mid-quiz.
    /// </summary>
    public List<Guid> QuestionOrder { get; set; } = [];
}

/// <summary>One saved answer, living inside the attempt's jsonb column.</summary>
public class StoredAnswer
{
    public Guid QuestionId { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = [];
    public string? Text { get; set; }

    public QuizAnswer ToAnswer() => new(QuestionId, SelectedOptionIds, Text);
}
