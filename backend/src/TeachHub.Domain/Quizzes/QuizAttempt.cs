using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

public enum QuizAttemptStatus { InProgress, Submitted, AutoSubmitted }

public class QuizAttempt : Entity
{
    public required Guid QuizId { get; set; }

    /// <summary>Gap #2: trỏ tới enrollment chứ không tới user — học lại kỳ sau là lượt khác.</summary>
    public required Guid EnrollmentId { get; set; }

    public int AttemptNo { get; set; } = 1;

    /// <summary>
    /// Máy chủ là nguồn chân lý về thời gian. Đồng hồ client sửa được bằng một dòng JavaScript,
    /// nên hạn giờ luôn tính từ mốc này cộng với time_limit của quiz.
    /// </summary>
    public required DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
    public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;

    /// <summary>
    /// Đáp án đã lưu, cập nhật sau MỖI câu chứ không đợi tới lúc nộp — lưu jsonb.
    /// Sinh viên rớt wifi giữa bài mà mất trắng sẽ đòi làm lại, và không có cách nào kiểm chứng.
    /// </summary>
    public List<StoredAnswer> Answers { get; set; } = [];

    public decimal? RawScore { get; set; }
    public decimal? MaxScore { get; set; }

    /// <summary>Điểm đã quy về thang 10 — con số chảy vào sổ điểm.</summary>
    public decimal? NormalizedScore { get; set; }

    /// <summary>Nguồn của hàng đợi ôn tập ngắt quãng (Phase 8).</summary>
    public List<Guid> WrongQuestionIds { get; set; } = [];

    /// <summary>
    /// Thứ tự câu đã xáo cho riêng lượt này. Lưu lại để tải lại trang không đảo thứ tự —
    /// xáo lại mỗi lần mở là sinh viên tưởng mình bị đổi đề giữa chừng.
    /// </summary>
    public List<Guid> QuestionOrder { get; set; } = [];
}

/// <summary>Một đáp án đã lưu, nằm trong cột jsonb của lượt làm.</summary>
public class StoredAnswer
{
    public Guid QuestionId { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = [];
    public string? Text { get; set; }

    public QuizAnswer ToAnswer() => new(QuestionId, SelectedOptionIds, Text);
}
