using TeachHub.Domain.Common;

namespace TeachHub.Domain.Groups;

/// <summary>
/// A group hangs off the COURSE OFFERING, not off the assignment. That makes "reuse the groups
/// from the previous assignment" (SRS section 5.5) free — no cloning groups per assignment.
/// </summary>
public class StudentGroup : Entity
{
    public required Guid OfferingId { get; set; }
    public required string Name { get; set; }
    public List<GroupMember> Members { get; set; } = [];
}

public class GroupMember : Entity
{
    public Guid GroupId { get; set; }

    /// <summary>
    /// Points at the enrollment, NOT at the user: a student retaking the subject in a later term
    /// is a different enrollment, and this term's groups must not bleed into that one.
    /// </summary>
    public required Guid EnrollmentId { get; set; }
}
