namespace TeachHub.Domain.Assignments;

public enum GroupAllocationMode { Manual, Random, ByStudentChoice }

/// <summary>
/// Cấu hình chia nhóm cho bài tập nộp theo nhóm. Lưu jsonb trên Assignment vì nó chỉ có
/// nghĩa cùng với bài tập đó và không bao giờ bị truy vấn độc lập. Việc chia nhóm thật
/// (GroupAllocator, bảng groups) thuộc Phase 4a.
/// </summary>
public class GroupConfig
{
    public int MinSize { get; set; } = 2;
    public int MaxSize { get; set; } = 5;
    public GroupAllocationMode AllocationMode { get; set; } = GroupAllocationMode.Manual;

    /// <summary>Cho phép chấm điểm riêng từng thành viên — mặc định cả nhóm cùng điểm.</summary>
    public bool AllowIndividualScores { get; set; }

    public GroupConfig DeepCopy() => new()
    {
        MinSize = MinSize,
        MaxSize = MaxSize,
        AllocationMode = AllocationMode,
        AllowIndividualScores = AllowIndividualScores,
    };
}
