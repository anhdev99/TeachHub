using TeachHub.Domain.Common;

namespace TeachHub.Domain.Learning;

public enum ProgressStatus { NotStarted, InProgress, Completed }

/// <summary>
/// An entirely new table (Gap #4). The SRS has <c>Chapter.unlock_rule</c>, has screen 6.3 with its
/// locked/unlocked icon and completion percentage, has XP and streaks — but has NO table storing
/// how far a student has got. Without it, none of the above can be built.
///
/// A student's learning data is NEVER deleted, not even soft-deleted.
/// </summary>
public class LessonProgress : Entity
{
    public required Guid EnrollmentId { get; set; }
    public required Guid LessonId { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public DateTimeOffset? CompletedAt { get; set; }
    public int SecondsSpent { get; set; }
}

public class ComponentProgress : Entity
{
    public required Guid EnrollmentId { get; set; }
    public required Guid LessonComponentId { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public DateTimeOffset? CompletedAt { get; set; }
}
