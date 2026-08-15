using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum GradeExportSource
{
    StudentCode,
    FullName,
    Email,
    ClassName,
    GradeColumn,
    BonusTotal,
    FinalScore,
    Blank,           // cột trống để phòng đào tạo tự điền
}

/// <summary>
/// Mẫu xuất của MỘT giảng viên, dùng lại được cho mọi lớp có cùng bộ key.
/// Mẫu thuộc giảng viên còn cột điểm thuộc lớp — nối hai bên bằng KEY, không bằng id.
/// Nối bằng id thì mẫu dựng công phu chỉ chạy cho đúng một lớp; sang lớp sau mọi cột có id
/// mới và mẫu xuất trống trơn, nên người dùng phải soạn lại mẫu cho từng lớp và tính năng
/// coi như không tồn tại.
/// </summary>
public class GradeExportTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public List<GradeExportItem> Items { get; set; } = [];
}

public class GradeExportItem : Entity
{
    public Guid TemplateId { get; set; }
    public required GradeExportSource Source { get; set; }

    /// <summary>Chỉ dùng khi Source = GradeColumn. Là KEY của cột, KHÔNG phải id.</summary>
    public string? ColumnKey { get; set; }

    /// <summary>Tiêu đề hiện trong file — sửa được cho khớp mẫu bảng điểm của trường.</summary>
    public required string Header { get; set; }

    public int OrderIndex { get; set; }
    public int DecimalPlaces { get; set; } = 1;
}

/// <summary>
/// Bản chốt bất biến khi lớp chuyển sang <c>Closed</c> (ngoại lệ của ADR-006 "không lưu điểm
/// tổng"). Chỉ ghi thêm, không sửa, không xóa — kể cả khi lớp được mở lại rồi đóng lần nữa.
/// </summary>
public class GradeSnapshot : Entity
{
    public required Guid OfferingId { get; set; }
    public required DateTimeOffset TakenAt { get; set; }
    public required Guid TakenBy { get; set; }

    /// <summary>Toàn bộ bảng điểm lúc chốt: cột, trọng số, quy tắc gộp, điều kiện, từng ô, điểm tổng.</summary>
    public required string PayloadJson { get; set; }

    /// <summary>SHA-256 của PayloadJson — đối chiếu về sau mà không phải tin vào trí nhớ.</summary>
    public required string PayloadHash { get; set; }
}
