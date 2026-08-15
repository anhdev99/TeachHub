using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Assessments;

/// <summary>Exam CHƯA hỗ trợ ở MVP nên cố ý không có trong enum — xem validator.</summary>
public enum FinalAssessmentType { Quiz, FinalProject }

/// <summary>
/// Đợt chấm cuối môn. Tái sử dụng NGUYÊN VẸN <see cref="SubmissionSlot"/>,
/// <c>Submission</c> và màn hình chấm của Phase 3 — chỉ khác <c>owner_type</c>.
/// Dựng một hệ thống nộp bài thứ hai cho bài tập lớn là cách chắc chắn nhất để hai chỗ
/// hành xử lệch nhau sau vài lần sửa.
/// </summary>
public class FinalAssessment : Entity
{
    public required Guid OfferingId { get; set; }
    public required FinalAssessmentType Type { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Gap #7: SRS để weight_percent ở CẢ đây LẪN GradeColumn — hai nguồn sự thật sẽ lệch
    /// nhau. Trọng số chỉ tồn tại ở GradeColumn; ở đây chỉ giữ con trỏ tới cột.
    ///
    /// Nullable, khác với plan ghi <c>required</c>: lúc tạo đợt cuối môn lớp có thể chưa
    /// có cột điểm nào (sổ điểm là Phase 4b), nên bắt buộc ngay từ đầu là bài toán con gà
    /// quả trứng — không tạo được đợt, mà cũng không có chỗ nào để tạo cột trước.
    /// Giao diện nhắc gắn cột; điểm chấm ra chỉ chảy vào sổ khi đã gắn.
    /// </summary>
    public Guid? GradeColumnId { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public SubmissionMode SubmissionMode { get; set; } = SubmissionMode.Individual;
    public GroupConfig? GroupConfig { get; set; }
    public Guid? RefQuizId { get; set; }                     // Phase 5
    public Guid? RubricId { get; set; }
    public decimal MaxScore { get; set; } = 10m;

    public List<SubmissionSlot> Slots { get; set; } = [];
}
