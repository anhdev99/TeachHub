namespace TeachHub.Application.Features.Learning;

/// <summary>
/// The grading rule for the inline questions of a <c>checkpoint_quiz</c> component. Pure function,
/// no I/O.
///
/// It exists as a SEPARATE place because this rule is read from two directions: the path that
/// builds the lesson payload (<c>InlineQuestionDto.MultiSelect</c> — the UI relies on it to decide
/// whether to render radio buttons or checkboxes) and the grading path in
/// <c>CheckInlineQuestionsCommand</c>. Two copies would drift apart on exactly the day someone adds
/// a second correct answer to a question, and the symptom would be the whole class never getting
/// that question right — no error, no log, nobody the wiser.
///
/// It lives in Application rather than Domain: this is not a grade-computing rule (inline questions
/// are SELF-CHECKS, they never reach the gradebook), and it only reads
/// <see cref="Domain.Content.InlineQuestion"/> rather than adding any behaviour to the entity. The
/// day inline questions start carrying marks, move it down into Domain.
/// </summary>
public static class InlineQuizGrader
{
    /// <summary>
    /// Multiple-answer is derived from THE DATA ITSELF, not from a hand-authored flag: a
    /// hand-authored flag can drift out of sync with the answer list, the number of correct
    /// answers cannot.
    /// </summary>
    public static bool IsMultiSelect(IReadOnlyCollection<int> correctIndexes) => correctIndexes.Count > 1;

    /// <summary>
    /// Compares SETS, not lists: the order in which the student clicked carries no meaning, and
    /// clicking the same box twice is still "picked that box".
    ///
    /// Picking one option too many is WRONG, even when every correct answer was picked as well —
    /// the same rule as Phase 5's <c>QuizGrader</c>. Loosening it means giving credit to whoever
    /// ticks every box.
    /// </summary>
    public static bool IsCorrect(IEnumerable<int> correctIndexes, IEnumerable<int>? selectedIndexes)
    {
        var correct = correctIndexes.ToHashSet();

        // A question authored without a correct answer cannot be answered correctly by anyone.
        // Returning true here is how a meaningless question unlocks the next lesson unnoticed.
        if (correct.Count == 0) return false;

        return correct.SetEquals(selectedIndexes ?? []);
    }
}
