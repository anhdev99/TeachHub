using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum BonusContextType { None, Assignment, FinalAssessment, Attendance }

/// <summary>
/// Thư viện lý do của MỘT giảng viên. Gắn vào giảng viên chứ không vào lớp: soạn một lần,
/// dùng lại cho mọi lớp và mọi học kỳ. Cấp điểm cộng là thao tác lặp lại nhiều nhất trong cả
/// hệ thống — gõ tay mỗi lần sẽ sinh ra hàng chục biến thể của cùng một lý do và làm hỏng
/// luôn thống kê sau này.
/// </summary>
public class BonusPointReasonTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Label { get; set; }
    public required decimal DefaultPoints { get; set; }
    public BonusTarget? DefaultTarget { get; set; }

    /// <summary>
    /// Đích mặc định trỏ bằng KEY chứ không bằng column_id: template thuộc giảng viên, còn cột
    /// thuộc lớp — mỗi lớp một bộ id mới. Trỏ bằng id thì lý do soạn sẵn chỉ dùng được cho
    /// đúng một lớp, tức là thư viện coi như vô dụng.
    /// </summary>
    public string? DefaultColumnKey { get; set; }

    public required int OrderIndex { get; set; }

    /// <summary>Ẩn thay vì xóa, để không phá lịch sử điểm cộng đã cấp.</summary>
    public bool IsActive { get; set; } = true;
}

public class BonusPoint : Entity
{
    /// <summary>LUÔN gắn 1 sinh viên cụ thể, kể cả khi đang chấm bài nhóm.</summary>
    public required Guid EnrollmentId { get; set; }

    public required Guid OfferingId { get; set; }
    public required BonusTarget Target { get; set; }

    /// <summary>BẮT BUỘC có khi Target = GradeColumn, BẮT BUỘC null khi Target = FinalTotal.</summary>
    public Guid? TargetColumnId { get; set; }

    /// <summary>Luôn &gt; 0. Muốn trừ điểm thì sửa điểm cột, không cấp điểm cộng âm.</summary>
    public required decimal Points { get; set; }

    /// <summary>null nếu giảng viên gõ lý do tự do. Chỉ để thống kê "lý do nào hay dùng".</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// BẮT BUỘC, không được rỗng, và là BẢN SAO văn bản tại thời điểm cấp — không phải chỉ
    /// một khóa ngoại tới template. Sáu tháng sau bạn sửa nhãn "Thuyết trình tự tin" thành
    /// "Thuyết trình xuất sắc" hoặc ẩn nó đi, và mọi điểm cộng đã cấp từ đầu kỳ lập tức đổi
    /// nghĩa hoặc mất chữ. Lúc sinh viên khiếu nại, thứ cần là lý do TẠI THỜI ĐIỂM CẤP.
    /// </summary>
    public required string Reason { get; set; }

    public BonusContextType ContextType { get; set; } = BonusContextType.None;
    public Guid? ContextId { get; set; }

    public required Guid GrantedBy { get; set; }

    /// <summary>Cùng với Id, đây là khóa sắp xếp tất định của <see cref="BonusLedger"/>.</summary>
    public required DateTimeOffset GrantedAt { get; set; }

    /// <summary>Cầu nối duy nhất sang tầng tính điểm — calculator không biết lý do và người cấp.</summary>
    public BonusEntry ToEntry() => new(Id, Target, TargetColumnId, Points, GrantedAt);
}
