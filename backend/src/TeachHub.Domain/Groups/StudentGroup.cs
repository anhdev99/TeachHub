using TeachHub.Domain.Common;

namespace TeachHub.Domain.Groups;

/// <summary>
/// Nhóm gắn vào LỚP HỌC PHẦN chứ không vào bài tập. Nhờ vậy "dùng lại nhóm của bài trước"
/// (SRS mục 5.5) là miễn phí — không phải nhân bản nhóm cho từng bài tập.
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
    /// Trỏ tới enrollment, KHÔNG phải user: một sinh viên học lại môn ở kỳ sau là một
    /// enrollment khác, và nhóm của kỳ này không được dính sang kỳ đó.
    /// </summary>
    public required Guid EnrollmentId { get; set; }
}
