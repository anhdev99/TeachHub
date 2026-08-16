using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

/// <summary>Phase 4b adds further targets (grade column, bonus point).</summary>
public enum GradeTargetType { Submission, GradeColumn, BonusPoint }

/// <summary>
/// The trail of every score change. An APPEND-ONLY table — no updates, no deletes, no
/// soft-delete query filter. This is what protects the instructor when a student disputes a
/// grade six months later: without it nobody can reconstruct the history of a number that has
/// since been overwritten.
/// </summary>
public class GradeAuditLog : Entity
{
    public required Guid OfferingId { get; set; }
    public required Guid EnrollmentId { get; set; }
    public required GradeTargetType TargetType { get; set; }
    public required Guid TargetId { get; set; }

    public decimal? OldValue { get; set; }
    public decimal? NewValue { get; set; }

    public required Guid ChangedBy { get; set; }
    public required DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }
}
