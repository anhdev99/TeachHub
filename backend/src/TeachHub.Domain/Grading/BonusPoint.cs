using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum BonusContextType { None, Assignment, FinalAssessment, Attendance }

/// <summary>
/// The reason library of ONE instructor. It hangs off the instructor, not off the class:
/// write it once, reuse it for every class and every term. Granting bonus points is the single
/// most repeated action in the whole system — typing the reason by hand every time produces
/// dozens of variants of the same reason and wrecks any statistics built on them later.
/// </summary>
public class BonusPointReasonTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Label { get; set; }
    public required decimal DefaultPoints { get; set; }
    public BonusTarget? DefaultTarget { get; set; }

    /// <summary>
    /// The default target points at a KEY, not at a column_id: the template belongs to the
    /// instructor while the column belongs to the class — every class gets a fresh set of ids.
    /// Point at an id and a pre-written reason only ever works for one single class, which
    /// makes the whole library useless.
    /// </summary>
    public string? DefaultColumnKey { get; set; }

    public required int OrderIndex { get; set; }

    /// <summary>Hide instead of delete, so the history of already-granted bonus points survives.</summary>
    public bool IsActive { get; set; } = true;
}

public class BonusPoint : Entity
{
    /// <summary>ALWAYS attached to 1 specific student, even while grading a group assignment.</summary>
    public required Guid EnrollmentId { get; set; }

    public required Guid OfferingId { get; set; }
    public required BonusTarget Target { get; set; }

    /// <summary>REQUIRED when Target = GradeColumn, REQUIRED to be null when Target = FinalTotal.</summary>
    public Guid? TargetColumnId { get; set; }

    /// <summary>Always &gt; 0. To take points away, edit the column score; never grant a negative bonus.</summary>
    public required decimal Points { get; set; }

    /// <summary>null when the instructor typed a free-form reason. Only used to track "which reasons get used".</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// REQUIRED, must not be empty, and is a COPY of the text as it stood at grant time — not
    /// merely a foreign key to the template. Six months later you rename the label
    /// "Confident presentation" to "Outstanding presentation" or hide it, and every bonus point
    /// granted since the start of term instantly changes meaning or loses its wording. When a
    /// student disputes a grade, what you need is the reason AS IT WAS AT GRANT TIME.
    /// </summary>
    public required string Reason { get; set; }

    public BonusContextType ContextType { get; set; } = BonusContextType.None;
    public Guid? ContextId { get; set; }

    public required Guid GrantedBy { get; set; }

    /// <summary>Together with Id, this is the deterministic sort key of <see cref="BonusLedger"/>.</summary>
    public required DateTimeOffset GrantedAt { get; set; }

    /// <summary>The only bridge into the scoring layer — the calculator knows nothing of reason or grantor.</summary>
    public BonusEntry ToEntry() => new(Id, Target, TargetColumnId, Points, GrantedAt);
}
