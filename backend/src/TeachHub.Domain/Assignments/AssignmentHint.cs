using TeachHub.Domain.Common;

namespace TeachHub.Domain.Assignments;

/// <summary>
/// Progressively unlocked hints for an assignment (SRS UC-09). Gap #11: the SRS declares hints
/// as a jsonb array on Assignment, but every time a student unlocks a hint that is an event
/// which has to be recordable (who unlocked it, when, how many points it cost) — and a jsonb
/// array has nothing to hang a foreign key off. Splitting it into its own table from day one is
/// far cheaper than migrating once there is real data.
/// </summary>
public class AssignmentHint : Entity
{
    public Guid AssignmentId { get; set; }
    public int OrderIndex { get; set; }
    public required string Content { get; set; }

    /// <summary>Percentage of the score deducted for unlocking this hint. 0 means it is free.</summary>
    public decimal PenaltyPercent { get; set; }

    public AssignmentHint Clone() => new()
    {
        OrderIndex = OrderIndex,
        Content = Content,
        PenaltyPercent = PenaltyPercent,
    };
}
