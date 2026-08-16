using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

/// <summary>
/// Liên kết một câu hỏi trong ngân hàng vào một quiz cụ thể. Bảng nối riêng chứ không nhân
/// bản câu hỏi: sửa một lỗi chính tả trong ngân hàng phải sửa đúng một chỗ.
/// </summary>
public class QuizItem : Entity
{
    public required Guid QuizId { get; set; }
    public required Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>Ghi đè điểm của câu trong riêng quiz này. Null = lấy điểm gốc của câu hỏi.</summary>
    public decimal? PointsOverride { get; set; }
}

/// <summary>
/// Cấu hình một quiz. Lưu thẳng trên <c>Quiz</c> (không tách bảng): mọi trường đều chỉ có
/// nghĩa cùng với quiz đó và không bao giờ bị truy vấn độc lập.
/// </summary>
public class QuizConfig
{
    /// <summary>
    /// false = checkpoint quiz: không tính điểm, hiện giải thích ngay sau khi trả lời.
    /// true = graded quiz: có hạn giờ, giới hạn số lần, điểm chảy vào sổ.
    /// </summary>
    public bool IsGraded { get; set; }

    /// <summary>null = không giới hạn thời gian.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>null = làm bao nhiêu lần cũng được.</summary>
    public int? MaxAttempts { get; set; }

    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }

    /// <summary>Hiện đáp án đúng + giải thích ngay sau khi nộp. Checkpoint quiz mặc định bật.</summary>
    public bool ShowExplanationAfterSubmit { get; set; } = true;

    public QuizConfig DeepCopy() => new()
    {
        IsGraded = IsGraded,
        TimeLimitMinutes = TimeLimitMinutes,
        MaxAttempts = MaxAttempts,
        ShuffleQuestions = ShuffleQuestions,
        ShuffleOptions = ShuffleOptions,
        ShowExplanationAfterSubmit = ShowExplanationAfterSubmit,
    };
}

/// <summary>
/// Luật thời gian của một lượt làm — hàm thuần để test được ca "hết giờ" mà không phải chờ
/// thời gian thật trôi qua.
/// </summary>
public static class QuizTiming
{
    /// <summary>
    /// Ân hạn 30 giây: mạng chậm, người dùng bấm nộp đúng giây cuối, hoặc đồng hồ máy chủ và
    /// trình duyệt lệch nhau vài giây. Không có ân hạn thì một bài nộp hợp lệ bị coi là hết giờ.
    /// </summary>
    public static readonly TimeSpan AnHan = TimeSpan.FromSeconds(30);

    public static DateTimeOffset? HanNop(DateTimeOffset startedAt, int? timeLimitMinutes) =>
        timeLimitMinutes is { } phut ? startedAt.AddMinutes(phut) : null;

    public static bool DaHetGio(DateTimeOffset startedAt, int? timeLimitMinutes, DateTimeOffset now) =>
        HanNop(startedAt, timeLimitMinutes) is { } han && now > han + AnHan;

    public static TimeSpan? ConLai(DateTimeOffset startedAt, int? timeLimitMinutes, DateTimeOffset now)
    {
        if (HanNop(startedAt, timeLimitMinutes) is not { } han) return null;
        var con = han - now;
        return con < TimeSpan.Zero ? TimeSpan.Zero : con;
    }
}
