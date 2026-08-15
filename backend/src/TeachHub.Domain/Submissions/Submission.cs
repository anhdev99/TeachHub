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

    public decimal? Score { get; set; }

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
