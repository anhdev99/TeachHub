using TeachHub.Domain.Common;
using TeachHub.Domain.Grading;

namespace TeachHub.Domain.Teaching;

public enum OfferingStatus { Draft, Active, Closed }

public class CourseOffering : Entity, IAuditable
{
    public required Guid SubjectId { get; set; }

    /// <summary>
    /// GHIM CỨNG tại thời điểm mở lớp. Giảng viên tạo phiên bản nội dung mới cho năm sau
    /// KHÔNG được làm đổi giá trị này — đó là toàn bộ lý do tồn tại của cơ chế versioning
    /// (SRS mục 5.2). Vì thế là init-only: sửa được nó là mất bất biến quan trọng nhất của hệ thống.
    /// </summary>
    public required Guid SubjectVersionId { get; init; }

    public required string Semester { get; set; }      // VD: HK1 2026-2027
    public required string ClassCode { get; set; }     // VD: DA24TIN01
    public string? JoinCodeValue { get; set; }
    public DateTimeOffset? JoinCodeExpiresAt { get; set; }
    public OfferingStatus Status { get; set; } = OfferingStatus.Draft;

    /// <summary>Hạn nộp hiển thị và nhập theo múi giờ này; lưu xuống DB luôn là UTC (Gap #16).</summary>
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    // ── Sổ điểm (Phase 4b) ──────────────────────────────────────────────────────────────

    /// <summary>
    /// CHỈ ghi nhận nguồn gốc ("lớp này mở từ mẫu nào"). KHÔNG phép tính nào đọc tới nó:
    /// cột điểm của lớp là bản SAO CHÉP đã cắt dây khỏi mẫu. Thấy một câu truy vấn join từ
    /// offering sang template để lấy trọng số thì đó là bug, không phải tối ưu.
    /// </summary>
    public Guid? GradeSchemeTemplateId { get; set; }

    /// <summary>Điều kiện điểm sàn của lớp — lưu jsonb.</summary>
    public List<GradeCondition> GradeConditions { get; set; } = [];

    public BonusTarget BonusDefaultTarget { get; set; } = BonusTarget.GradeColumn;
    public Guid? BonusDefaultColumnId { get; set; }

    /// <summary>null = không đặt trần riêng cho lớp này.</summary>
    public decimal? BonusMaxPointsPerStudent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Mã đúng vẫn CHƯA đủ để vào lớp — người gọi còn phải có tên trong danh sách
    /// (kiểm ở handler). Mã lớp bị lan ra ngoài là chuyện chắc chắn xảy ra.
    /// </summary>
    public bool AcceptsJoinCode(string code, DateTimeOffset now) =>
        Status == OfferingStatus.Active
        && JoinCodeValue is not null
        && string.Equals(JoinCodeValue, code, StringComparison.OrdinalIgnoreCase)
        && (JoinCodeExpiresAt is null || now < JoinCodeExpiresAt);
}
