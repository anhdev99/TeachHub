using TeachHub.Domain.Common;

namespace TeachHub.Domain.Teaching;

public enum EnrollmentStatus { Imported, Active, Dropped }

public class Enrollment : Entity, IAuditable
{
    public required Guid OfferingId { get; set; }
    public required string StudentCode { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? ClassName { get; set; }

    /// <summary>
    /// Null cho tới khi sinh viên kích hoạt tài khoản (Gap #2): danh sách import từ Excel
    /// có trước, tài khoản có sau. Ghép lại lúc SV nhập mã lớp hoặc lúc giảng viên
    /// tạo tài khoản cho cả lớp.
    /// </summary>
    public Guid? UserId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Imported;

    // Động lực học tập — Phase 7 mới dùng tới, giữ chỗ để không phải migrate lại.
    public int XpPoints { get; set; }
    public int StreakDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
