using System.Globalization;
using System.Text;

namespace TeachHub.Domain.Quizzes;

/// <summary>
/// A student's answer to one question. <paramref name="SelectedOptionIds"/> is for choice
/// questions, <paramref name="Text"/> is for fill-in-the-blank questions.
///
/// An answer ALWAYS carries the real ids of the question and of the options, never a positional
/// index: shuffling happens purely in the presentation layer, so the client's positional index
/// means nothing on the server side and would grade the wrong thing the very first time
/// shuffling is switched on.
/// </summary>
public sealed record QuizAnswer(Guid QuestionId, List<Guid> SelectedOptionIds, string? Text);

public sealed record QuestionResult(Guid QuestionId, bool IsCorrect, decimal Earned, decimal Max);

public sealed record QuizGradeResult(
    decimal RawScore, decimal MaxScore, List<QuestionResult> PerQuestion)
{
    /// <summary>
    /// Normalized onto the 0–10 scale — exactly the rule Phase 4b settled on for the Assignment source.
    /// A 20-question quiz has a natural scale of 20; feeding that raw number straight into a grade
    /// column and then clamping at 10 turns every student who got 10 or more questions right into a 10.
    /// </summary>
    public decimal NormalizedScore =>
        MaxScore <= 0m ? 0m : Math.Round(RawScore / MaxScore * 10m, 2, MidpointRounding.AwayFromZero);

    public List<Guid> WrongQuestionIds => [.. PerQuestion.Where(p => !p.IsCorrect).Select(p => p.QuestionId)];
}

/// <summary>Pure functions, no I/O. Every rule for grading a quiz lives here.</summary>
public static class QuizGrader
{
    public static QuizGradeResult Grade(
        IReadOnlyList<QuizQuestion> questions, IReadOnlyList<QuizAnswer> answers)
    {
        var results = new List<QuestionResult>(questions.Count);

        foreach (var q in questions)
        {
            var answer = answers.FirstOrDefault(a => a.QuestionId == q.Id);
            var earned = GradeOneQuestion(q, answer);
            results.Add(new QuestionResult(q.Id, earned >= q.Points && q.Points > 0m, earned, q.Points));
        }

        return new QuizGradeResult(
            Math.Round(results.Sum(r => r.Earned), 2, MidpointRounding.AwayFromZero),
            questions.Sum(q => q.Points),
            results);
    }

    private static decimal GradeOneQuestion(QuizQuestion q, QuizAnswer? answer) => q.Type switch
    {
        QuestionType.FillBlank => GradeFillBlank(q, answer?.Text),
        QuestionType.MultipleChoice => GradeMultipleChoice(q, answer),
        _ => ChamMotDapAn(q, answer),                    // SingleChoice and TrueFalse share the rule
    };

    private static decimal ChamMotDapAn(QuizQuestion q, QuizAnswer? answer)
    {
        // Selecting more than one option on a single-choice question is an invalid answer, not
        // "they picked exactly one of them". Blindly taking the first option would be going easy
        // on someone who is guessing.
        if (answer?.SelectedOptionIds is not { Count: 1 }) return 0m;

        var selected = answer.SelectedOptionIds[0];
        return q.Options.Any(o => o.Id == selected && o.IsCorrect) ? q.Points : 0m;
    }

    private static decimal GradeMultipleChoice(QuizQuestion q, QuizAnswer? answer)
    {
        var correct = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
        var selected = (answer?.SelectedOptionIds ?? []).ToHashSet();

        // Drop options that do not belong to this question: unknown ids come from a client that
        // tampered with the payload, and they must not move the score in any direction.
        selected.IntersectWith(q.Options.Select(o => o.Id));

        if (correct.Count == 0) return 0m;

        if (!q.AllowPartialCredit)
            return selected.SetEquals(correct) ? q.Points : 0m;

        // Partial credit: every correct option adds, every wrong option SUBTRACTS the same amount —
        // without the subtraction, selecting every option would score full marks.
        var correctCount = selected.Count(c => correct.Contains(c));
        var wrongCount = selected.Count - correctCount;
        var ratio = (correctCount - wrongCount) / (decimal)correct.Count;

        return Math.Max(0m, Math.Round(q.Points * ratio, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal GradeFillBlank(QuizQuestion q, string? answer)
    {
        if (q.FillBlank is not { } cfg || answer is null) return 0m;

        var submitted = Normalize(answer, cfg);

        return cfg.AcceptedAnswers.Any(d => string.Equals(Normalize(d, cfg), submitted, StringComparison.Ordinal))
            ? q.Points
            : 0m;
    }

    /// <summary>
    /// Normalizes BOTH the accepted answers AND the student's submission through the same function.
    /// Normalizing only one side compares two things that are not in the same form, and the failing
    /// case would then depend on which input method the instructor used to type the answer — the
    /// last thing anyone thinks of when hunting for the cause.
    /// </summary>
    private static string Normalize(string s, FillBlankConfig cfg)
    {
        // NFC FIRST: "ế" typed with Telex is U+0065 U+0302 U+0301 (e + circumflex + acute), while
        // typed on a macOS keyboard it is U+1EBF (a single precomposed character). They look
        // exactly alike; compared byte by byte they differ.
        if (cfg.NormalizeUnicode) s = s.Normalize(NormalizationForm.FormC);

        if (cfg.TrimWhitespace)
        {
            // Collapse every run of whitespace into a single space, then trim both ends: "Hà  Nội"
            // and "Hà Nội " have to grade the same — an extra space is a typing slip, not a gap in
            // knowledge.
            s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        if (!cfg.CaseSensitive) s = s.ToLower(CultureInfo.InvariantCulture);

        return s;
    }
}
