using TeachHub.Domain.Common;

namespace TeachHub.Domain.Review;

/// <summary>
/// Một khái niệm sinh viên đã trả lời sai và cần ôn lại.
///
/// Gap #2 — gắn với <see cref="UserId"/> chứ KHÔNG gắn với enrollment_id, khác mọi entity còn
/// lại của dự án. Kiến thức nền tảng thuộc về con người, không thuộc về một lớp học phần: sinh
/// viên sai "con trỏ" ở môn Lập trình C rồi học tiếp môn Cấu trúc dữ liệu vẫn phải được ôn lại
/// đúng khái niệm đó. Gắn vào enrollment là xóa sạch hàng đợi mỗi lần kết thúc học kỳ, và toàn
/// bộ giá trị của ôn tập ngắt quãng nằm ở chỗ nó kéo dài qua nhiều học kỳ.
/// Đừng "sửa cho nhất quán" với các entity khác.
/// </summary>
public class ReviewQueueItem : Entity, IAuditable
{
    public required Guid UserId { get; set; }
    public required Guid QuestionId { get; set; }

    /// <summary>
    /// Lượt làm quiz đã sinh ra mục này — chỉ để lần vết khi cần giải thích cho sinh viên vì
    /// sao câu này nằm trong hàng đợi. Nullable và KHÔNG bao giờ trỏ lại mục cũ khi mục được
    /// nạp lại: một mục sống lâu hơn lượt làm sinh ra nó.
    /// </summary>
    public Guid? SourceQuizAttemptId { get; set; }

    public ReviewStage Stage { get; set; } = ReviewStage.ThreeDays;

    /// <summary>
    /// Luôn đi cùng <see cref="Stage"/> và CHỈ được đổi qua <see cref="ApplyAnswer"/>. Gán tay
    /// hai trường này ở hai chỗ khác nhau là cách chắc chắn nhất để chúng lệch nhau, và một
    /// mục Stage=Mastered mà Status=Pending sẽ hiện mãi trong "Ôn tập hôm nay" với NextReviewAt
    /// null — tức là không bao giờ đến hạn mà cũng không bao giờ biến mất.
    /// </summary>
    public ReviewItemStatus Status { get; set; } = ReviewItemStatus.Pending;

    /// <summary>null = đã thuộc, không còn lần ôn nào nữa.</summary>
    public DateTimeOffset? NextReviewAt { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Mục mới sinh từ MỘT câu trả lời sai, nên <see cref="WrongCount"/> bắt đầu từ 1 chứ không
    /// từ 0 — đếm từ 0 là làm mất chính sự kiện đã sinh ra mục này, và về sau không giải thích
    /// được vì sao một mục chưa bao giờ sai lại nằm trong hàng đợi.
    /// </summary>
    public static ReviewQueueItem Create(
        Guid userId, Guid questionId, Guid? sourceQuizAttemptId, DateTimeOffset now)
    {
        var lich = ReviewSchedule.Start(now);

        return new ReviewQueueItem
        {
            UserId = userId,
            QuestionId = questionId,
            SourceQuizAttemptId = sourceQuizAttemptId,
            Stage = lich.Stage,
            Status = ReviewItemStatus.Pending,
            NextReviewAt = lich.NextReviewAt,
            WrongCount = 1,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Ghi nhận một lần trả lời và dời lịch. Đây là đường DUY NHẤT đổi
    /// <see cref="Stage"/>/<see cref="Status"/>/<see cref="NextReviewAt"/> — ba trường đó phải
    /// đổi cùng nhau, luôn luôn.
    /// </summary>
    public void ApplyAnswer(bool dung, DateTimeOffset now)
    {
        var lich = ReviewSchedule.Next(Stage, dung, now);

        Stage = lich.Stage;
        Status = lich.IsMastered ? ReviewItemStatus.Mastered : ReviewItemStatus.Pending;
        NextReviewAt = lich.NextReviewAt;
        LastReviewedAt = now;
        UpdatedAt = now;

        if (dung) CorrectCount++;
        else WrongCount++;
    }

    /// <summary>
    /// Đến hạn ôn chưa. Điều kiện này lặp lại nguyên văn trong câu truy vấn "Ôn tập hôm nay" và
    /// trong partial index của bảng — sửa ở đây thì phải sửa cả hai chỗ kia.
    /// </summary>
    public bool IsDue(DateTimeOffset now) =>
        Status == ReviewItemStatus.Pending && NextReviewAt is { } han && han <= now;
}

/// <summary>
/// Đánh dấu một lượt làm quiz ĐÃ được nạp vào hàng đợi ôn tập.
///
/// Không có bảng này thì job quét hằng ngày không phân biệt được "lượt này chưa nạp" với "lượt
/// này nạp rồi nhưng cả 3 câu sai đều đã có sẵn mục trong hàng đợi" — và nó sẽ nạp lại lượt đó
/// mỗi đêm, mỗi lần đều tính là trả lời SAI, tức là đẩy bậc của sinh viên về 3 ngày vĩnh viễn.
/// Lịch ôn không bao giờ tiến lên được, không có exception nào, không có test nào đỏ.
/// </summary>
public class ReviewGenerationMark : Entity
{
    public required Guid QuizAttemptId { get; set; }
    public required DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Số mục mới tạo và số mục đã có bị đẩy về đầu chu kỳ — chỉ để đọc log.</summary>
    public int ItemsCreated { get; set; }
    public int ItemsReset { get; set; }
}
