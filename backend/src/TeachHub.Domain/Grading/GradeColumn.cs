using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum GradeSourceType { Assignment, Quiz, FinalAssessment }

/// <summary>
/// Cột điểm của MỘT lớp. Được SAO CHÉP từ <see cref="GradeSchemeTemplate"/> lúc mở lớp rồi
/// sống độc lập: sửa mẫu ở học kỳ sau KHÔNG được đổi cột điểm của lớp đã dạy xong.
///
/// Cùng nguyên tắc với ADR-004 (lớp ghim SubjectVersion). Nếu cột chỉ là con trỏ tới mẫu thì
/// đổi mẫu từ 20/30/50 sang 30/20/50 sẽ đổi luôn bảng điểm đã nộp của kỳ trước — không ai bấm
/// nút nào, và không có gì trong hệ thống nói cho bạn biết.
/// </summary>
public class GradeColumn : Entity
{
    public required Guid OfferingId { get; set; }

    /// <summary>
    /// Slug ổn định (<c>thuong_ky</c>, <c>cuoi_mon</c>). Mẫu xuất Excel, điều kiện điểm sàn và
    /// thư viện lý do điểm cộng đều nối vào cột bằng KEY này, không bằng Id: chúng thuộc về
    /// giảng viên còn cột thuộc về lớp, nên nối bằng id thì mỗi mẫu chỉ dùng được cho đúng
    /// một lớp — tức là tính năng coi như không tồn tại.
    /// </summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public required decimal WeightPercent { get; set; }
    public int OrderIndex { get; set; }
    public GradeAggregation Aggregation { get; set; } = new();
    public List<GradeColumnSource> Sources { get; set; } = [];
}

public class GradeColumnSource : Entity
{
    public required Guid GradeColumnId { get; set; }
    public required GradeSourceType SourceType { get; set; }
    public required Guid SourceId { get; set; }

    /// <summary>Trọng số của nguồn này TRONG cột, chỉ dùng khi Kind = WeightedAverage. Null = chia đều.</summary>
    public decimal? WeightWithinColumn { get; set; }
}

/// <summary>Điểm của một sinh viên ở một cột. KHÔNG lưu điểm tổng ở đâu cả (ADR-006).</summary>
public class GradeRecord : Entity
{
    public required Guid EnrollmentId { get; set; }
    public required Guid GradeColumnId { get; set; }
    public decimal? Score { get; set; }

    /// <summary>
    /// true = giảng viên đã sửa tay ô này, và việc chấm lại một bài trong cột KHÔNG được ghi đè.
    /// Thiếu cờ này thì điểm giảng viên điều chỉnh sẽ âm thầm biến mất khi chấm lại, và không
    /// ai phát hiện cho tới lúc sinh viên hỏi.
    /// </summary>
    public bool IsManualOverride { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Thư viện bộ cột mẫu của MỘT giảng viên. Gắn vào giảng viên chứ không vào môn học:
/// soạn một lần, dùng cho mọi môn và mọi học kỳ về sau.
/// </summary>
public class GradeSchemeTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public int OrderIndex { get; set; }
    public List<GradeSchemeTemplateItem> Items { get; set; } = [];
}

public class GradeSchemeTemplateItem : Entity
{
    public Guid TemplateId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public required decimal WeightPercent { get; set; }
    public int OrderIndex { get; set; }
    public GradeAggregation Aggregation { get; set; } = new();
}

/// <summary>
/// Bộ cột mặc định khi giảng viên chưa soạn mẫu nào. Hai cột là cách chia phổ biến nhất ở
/// đại học Việt Nam, và có sẵn một bộ chạy được quan trọng hơn là bắt người dùng cấu hình
/// trước khi thấy sổ điểm lần đầu.
/// </summary>
public static class GradeColumnFactory
{
    public static List<GradeColumn> MacDinh(Guid offeringId) =>
    [
        new()
        {
            OfferingId = offeringId, Key = "thuong_ky", Name = "Thường kỳ",
            WeightPercent = 40m, OrderIndex = 0,
            Aggregation = new GradeAggregation { Kind = AggregationKind.Average },
        },
        new()
        {
            OfferingId = offeringId, Key = "cuoi_mon", Name = "Cuối môn",
            WeightPercent = 60m, OrderIndex = 1,
            Aggregation = new GradeAggregation { Kind = AggregationKind.Average },
        },
    ];
}
