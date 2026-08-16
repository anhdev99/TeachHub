using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

public enum QuestionType { SingleChoice, MultipleChoice, TrueFalse, FillBlank }

/// <summary>
/// A question in the bank of ONE instructor. Attached to the instructor rather than to the
/// subject so it can be reused across several subjects and several terms — same reason as
/// Rubric (Gap #18).
/// </summary>
public class QuizQuestion : Entity
{
    public required Guid InstructorId { get; set; }
    public required QuestionType Type { get; set; }
    public required string Text { get; set; }
    public string? Explanation { get; set; }

    /// <summary>
    /// Foundation-knowledge tags, used by the <c>recap</c> component and by the spaced review
    /// queue (Phase 8): "pick 3–5 questions carrying the previous chapter's tags".
    /// </summary>
    public List<string> FoundationTags { get; set; } = [];

    /// <summary>Points this question is worth inside a quiz. A quiz's natural scale is the sum of its questions' points.</summary>
    public decimal Points { get; set; } = 1m;

    public List<QuestionOption> Options { get; set; } = [];

    /// <summary>Only meaningful for <see cref="QuestionType.FillBlank"/> — stored as jsonb.</summary>
    public FillBlankConfig? FillBlank { get; set; }

    /// <summary>
    /// Partial credit for <see cref="QuestionType.MultipleChoice"/>. Defaults to FALSE —
    /// all-or-nothing, because that is the rule that is easiest to explain to a student. Made
    /// configurable per question instead of hard-wired system-wide: both grading styles are
    /// legitimate, and picking one for good forces every instructor to live with the decision
    /// of whoever wrote the code.
    /// </summary>
    public bool AllowPartialCredit { get; set; }
}

public class QuestionOption : Entity
{
    public Guid QuestionId { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// Grading configuration for a fill-in-the-blank question. Gap #12: grading Vietnamese cannot be
/// a raw string comparison.
/// </summary>
public class FillBlankConfig
{
    public List<string> AcceptedAnswers { get; set; } = [];
    public bool CaseSensitive { get; set; }
    public bool TrimWhitespace { get; set; } = true;

    /// <summary>
    /// Normalize Unicode to NFC. Defaults to ON and should essentially never be turned off:
    /// "tiếng Việt" typed with Telex and typed on a macOS keyboard produce two DIFFERENT Unicode
    /// strings that look exactly alike (base character + combining marks, versus precomposed
    /// characters). Without normalization a student who answered correctly is still marked wrong,
    /// and nobody can see why by reading the two strings.
    /// </summary>
    public bool NormalizeUnicode { get; set; } = true;

    public FillBlankConfig DeepCopy() => new()
    {
        AcceptedAnswers = [.. AcceptedAnswers],
        CaseSensitive = CaseSensitive,
        TrimWhitespace = TrimWhitespace,
        NormalizeUnicode = NormalizeUnicode,
    };
}
