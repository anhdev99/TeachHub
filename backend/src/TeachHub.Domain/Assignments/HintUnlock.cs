using TeachHub.Domain.Common;

namespace TeachHub.Domain.Assignments;

/// <summary>
/// Nhật ký "sinh viên nào mở gợi ý nào lúc nào" (Gap #11). Đây là lý do <see cref="AssignmentHint"/>
/// phải là bảng riêng chứ không phải mảng jsonb trên Assignment: một mảng jsonb không gắn khóa
/// ngoại vào đâu được, nên không ghi lại được sự kiện mở.
/// </summary>
public class HintUnlock : Entity
{
    public required Guid AssignmentHintId { get; set; }

    /// <summary>
    /// Trỏ tới enrollment, KHÔNG phải user (Gap #2). Sinh viên học lại môn ở kỳ sau là một
    /// enrollment khác; trỏ vào user thì gợi ý đã mở kỳ trước vẫn trừ điểm ở kỳ này.
    /// </summary>
    public required Guid EnrollmentId { get; set; }

    public required DateTimeOffset UnlockedAt { get; set; }

    /// <summary>
    /// Chép lại <see cref="AssignmentHint.PenaltyPercent"/> tại đúng thời điểm mở.
    ///
    /// Không chép mà đọc thẳng từ AssignmentHint lúc chấm thì giảng viên sửa mức phạt sau khi
    /// nửa lớp đã mở gợi ý sẽ đổi ngược lại điều kiện mà những sinh viên đó đã chấp nhận —
    /// họ bấm "mở" khi màn hình ghi trừ 10%, rồi bị trừ 30%. Không có cách nào giải thích được
    /// chuyện đó, và cũng không có cách nào phát hiện ra ngoài lời khiếu nại.
    /// </summary>
    public decimal PenaltyPercentApplied { get; set; }
}
