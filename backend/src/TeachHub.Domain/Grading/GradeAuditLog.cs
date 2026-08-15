using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

/// <summary>Phase 4b mở rộng thêm đích khác (cột điểm, điểm cộng).</summary>
public enum GradeTargetType { Submission, GradeColumn, BonusPoint }

/// <summary>
/// Vết của mọi lần điểm đổi. Bảng CHỈ GHI THÊM — không sửa, không xóa, không query filter
/// soft-delete. Đây là thứ bảo vệ giảng viên khi sinh viên khiếu nại điểm sáu tháng sau:
/// không có nó thì không ai dựng lại được lịch sử một con số đã bị ghi đè.
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
