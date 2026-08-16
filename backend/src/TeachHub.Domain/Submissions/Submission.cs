using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Submissions;

public enum SubmissionStatus { Draft, Submitted, Grading, Graded, Returned }

public enum SubmittedByType { Individual, Group }

public class Submission : Entity
{
    public SlotOwnerType OwnerType { get; set; } = SlotOwnerType.Assignment;

    // ADR-003: hai FK thật + CHECK constraint, không dùng owner_id đa hình.
    public Guid? AssignmentId { get; set; }
    public Guid? FinalAssessmentId { get; set; }

    public required Guid OfferingId { get; set; }

    public SubmittedByType SubmittedByType { get; set; } = SubmittedByType.Individual;

    /// <summary>
    /// Gap #2: khóa ngoại trỏ tới enrollment, KHÔNG phải user. Cùng một sinh viên học lại
    /// môn này ở học kỳ sau là một enrollment khác — trỏ vào user thì hai lần học nhập
    /// vào nhau và điểm kỳ trước đè lên kỳ này.
    /// </summary>
    public Guid? EnrollmentId { get; set; }

    public Guid? GroupId { get; set; }          // Phase 4a

    /// <summary>Gap #5: nộp lại tạo attempt mới, bản cũ giữ nguyên với IsCurrent = false.</summary>
    public int AttemptNo { get; set; } = 1;

    public bool IsCurrent { get; set; } = true;

    public DateTimeOffset? SubmittedAt { get; set; }
    public bool IsLate { get; set; }
    public int HintsUsed { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Draft;

    /// <summary>Điểm HIỆU LỰC — đã trừ gợi ý và nộp trễ. Sổ điểm đọc đúng cột này.</summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Điểm giảng viên chấm, TRƯỚC mọi mức phạt (Phase 6b).
    ///
    /// Không có cột này thì mức phạt chỉ áp được đúng một lần: lần thứ hai sẽ trừ chồng lên
    /// điểm đã trừ, nên mỗi lần giảng viên sửa hạn nộp rồi bấm "tính lại" là điểm sinh viên
    /// tụt thêm một nấc, không có gì báo và không có đường quay lại.
    ///
    /// null với các bài chấm trước Phase 6 — lúc đó <see cref="Score"/> chính là điểm gốc.
    /// </summary>
    public decimal? RawScore { get; set; }

    /// <summary>
    /// Tỉ lệ đã trừ, lưu lại để giải thích được "10 điểm − 20% gợi ý − 10% trễ = 7.2" mà
    /// không phải tính lại từ hạn nộp và nhật ký mở gợi ý mỗi lần hiển thị. Sinh viên khiếu
    /// nại điểm sau ba tháng thì hạn nộp có thể đã bị sửa, và con số tính lại sẽ khác con số
    /// đã trả về cho họ.
    /// </summary>
    public decimal LatePenaltyPercent { get; set; }

    public decimal HintPenaltyPercent { get; set; }

    /// <summary>Số ngày trễ tại thời điểm áp phạt — hiển thị kèm để giảng viên đối chiếu.</summary>
    public int DaysLate { get; set; }

    /// <summary>Điểm riêng từng thành viên nhóm (UC-10b). Rỗng ⇒ cả nhóm nhận điểm chung.</summary>
    public List<SubmissionMemberScore> MemberScores { get; set; } = [];

    /// <summary>criterion_id → điểm, lưu jsonb. Phase 4a dùng để chấm theo rubric.</summary>
    public Dictionary<Guid, decimal> RubricScores { get; set; } = [];

    public string? Feedback { get; set; }
    public Guid? GradedBy { get; set; }
    public DateTimeOffset? GradedAt { get; set; }

    public List<SubmissionFile> Files { get; set; } = [];

    /// <summary>
    /// So sánh hạn nộp nhận <paramref name="now"/> từ ngoài chứ không đọc
    /// <c>DateTimeOffset.UtcNow</c>: mọi logic phụ thuộc thời gian đi qua TimeProvider,
    /// nếu không thì kiểm tra một ca "nộp trễ" phải chờ thời gian thật trôi qua.
    /// </summary>
    public void MarkSubmitted(DateTimeOffset now, DateTimeOffset? dueAt)
    {
        SubmittedAt = now;
        IsLate = dueAt is not null && now > dueAt;
        Status = SubmissionStatus.Submitted;
    }
}
