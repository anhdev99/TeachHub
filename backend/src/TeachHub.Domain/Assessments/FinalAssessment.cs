using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Assessments;

/// <summary>Exam is NOT supported in the MVP, so it is deliberately absent from the enum — see the validator.</summary>
public enum FinalAssessmentType { Quiz, FinalProject }

/// <summary>
/// End-of-course assessment. Reuses <see cref="SubmissionSlot"/>, <c>Submission</c> and the
/// Phase 3 grading screen UNCHANGED — only <c>owner_type</c> differs. Standing up a second
/// submission system just for the final project is the surest way to make the two of them
/// behave differently after a few rounds of edits.
/// </summary>
public class FinalAssessment : Entity
{
    public required Guid OfferingId { get; set; }
    public required FinalAssessmentType Type { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Gap #7: the SRS puts weight_percent BOTH here AND on GradeColumn — two sources of truth
    /// that are going to drift apart. The weight lives only on GradeColumn; this field holds
    /// nothing but a pointer to the column.
    ///
    /// Nullable, unlike the <c>required</c> the plan calls for: at the time the final assessment
    /// is created the class may not have a single grade column yet (the gradebook is Phase 4b),
    /// so requiring one up front is a chicken-and-egg problem — you cannot create the assessment,
    /// and there is nowhere to create the column first either. The UI prompts to attach a column;
    /// the resulting scores only flow into the gradebook once one is attached.
    /// </summary>
    public Guid? GradeColumnId { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public SubmissionMode SubmissionMode { get; set; } = SubmissionMode.Individual;
    public GroupConfig? GroupConfig { get; set; }
    public Guid? RefQuizId { get; set; }                     // Phase 5
    public Guid? RubricId { get; set; }
    public decimal MaxScore { get; set; } = 10m;

    public List<SubmissionSlot> Slots { get; set; } = [];
}
