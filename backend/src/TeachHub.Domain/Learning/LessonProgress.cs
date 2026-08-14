using TeachHub.Domain.Common;

namespace TeachHub.Domain.Learning;

public enum ProgressStatus { NotStarted, InProgress, Completed }

/// <summary>
/// Bảng mới hoàn toàn (Gap #4). SRS có <c>Chapter.unlock_rule</c>, có màn hình 6.3 với icon
/// khóa/mở và % hoàn thành, có XP và streak — nhưng KHÔNG có bảng nào lưu sinh viên đã học
/// tới đâu. Không có nó thì không mục nào ở trên làm được.
///
/// Dữ liệu học tập của sinh viên KHÔNG BAO GIỜ bị xóa, kể cả xóa mềm.
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
