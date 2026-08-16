using TeachHub.Domain.Content;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Application.Features.Quizzes;

public sealed record QuestionOptionDto(Guid Id, string Text, bool IsCorrect, int OrderIndex);

public sealed record FillBlankDto(
    List<string> AcceptedAnswers, bool CaseSensitive, bool TrimWhitespace, bool NormalizeUnicode);

public sealed record QuizQuestionDto(
    Guid Id, QuestionType Type, string Text, string? Explanation,
    List<string> FoundationTags, decimal Points, bool AllowPartialCredit,
    List<QuestionOptionDto> Options, FillBlankDto? FillBlank)
{
    public static QuizQuestionDto From(QuizQuestion q) => new(
        q.Id, q.Type, q.Text, q.Explanation, q.FoundationTags, q.Points, q.AllowPartialCredit,
        [.. q.Options.OrderBy(o => o.OrderIndex)
             .Select(o => new QuestionOptionDto(o.Id, o.Text, o.IsCorrect, o.OrderIndex))],
        q.FillBlank is null
            ? null
            : new FillBlankDto(q.FillBlank.AcceptedAnswers, q.FillBlank.CaseSensitive,
                               q.FillBlank.TrimWhitespace, q.FillBlank.NormalizeUnicode));
}

public sealed record QuizConfigDto(
    bool IsGraded, int? TimeLimitMinutes, int? MaxAttempts,
    bool ShuffleQuestions, bool ShuffleOptions, bool ShowExplanationAfterSubmit);

public sealed record QuizDto(
    Guid Id, Guid? SubjectVersionId, Guid? OfferingId, string Title, string? Description,
    QuizConfigDto Config, int QuestionCount, decimal TotalPoints,
    List<QuizQuestionDto> Questions)
{
    public static QuizDto From(Quiz q, List<QuizQuestion> cau) => new(
        q.Id, q.SubjectVersionId, q.OfferingId, q.Title, q.Description,
        new QuizConfigDto(q.Config.IsGraded, q.Config.TimeLimitMinutes, q.Config.MaxAttempts,
                          q.Config.ShuffleQuestions, q.Config.ShuffleOptions,
                          q.Config.ShowExplanationAfterSubmit),
        cau.Count, cau.Sum(c => c.Points),
        [.. cau.Select(QuizQuestionDto.From)]);
}

// ── Phía sinh viên: KHÔNG bao giờ kèm đáp án đúng ───────────────────────────────────────

/// <summary>
/// Phương án hiện cho sinh viên. Cố ý KHÔNG có <c>IsCorrect</c>: chỉ cần một lần lỡ trả
/// trường đó là cả lớp mở DevTools ra xem đáp án, và không có gì trong hệ thống báo động.
/// Đây là lý do phía sinh viên dùng DTO riêng chứ không tái sử dụng <see cref="QuestionOptionDto"/>.
/// </summary>
public sealed record TakeOptionDto(Guid Id, string Text);

public sealed record TakeQuestionDto(
    Guid Id, QuestionType Type, string Text, decimal Points, List<TakeOptionDto> Options);

public sealed record TakeAnswerDto(Guid QuestionId, List<Guid> SelectedOptionIds, string? Text);

public sealed record TakeAttemptDto(
    Guid Id, Guid QuizId, string Title, string? Description, int AttemptNo,
    QuizAttemptStatus Status, DateTimeOffset StartedAt, DateTimeOffset? DueAt,
    int? SecondsRemaining, bool IsGraded,
    List<TakeQuestionDto> Questions, List<TakeAnswerDto> Answers,
    decimal? RawScore, decimal? MaxScore, decimal? NormalizedScore,
    List<QuestionReviewDto> Review);

/// <summary>Chỉ trả về SAU khi đã nộp, và chỉ khi quiz bật hiện giải thích.</summary>
public sealed record QuestionReviewDto(
    Guid QuestionId, bool IsCorrect, decimal Earned, decimal Max,
    string? Explanation, List<Guid> CorrectOptionIds, List<string> AcceptedAnswers);
