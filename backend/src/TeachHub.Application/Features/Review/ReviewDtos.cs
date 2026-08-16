using TeachHub.Application.Features.Quizzes;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// Một mục trong hàng đợi ôn tập, kèm câu hỏi để sinh viên trả lời ngay tại chỗ.
///
/// Dùng LẠI <see cref="TakeQuestionDto"/> của Phase 5 chứ không dựng một DTO câu hỏi riêng:
/// kiểu đó cố ý KHÔNG có trường <c>IsCorrect</c> trên phương án. Một DTO song song viết mới
/// là một chỗ nữa có thể lỡ tay trả đáp án đúng ra ngoài, và lần đó sẽ không ai phát hiện —
/// dữ liệu vẫn hiển thị bình thường, chỉ là mở DevTools ra là thấy đáp án.
/// </summary>
public sealed record ReviewItemDto(
    Guid Id, Guid QuestionId, ReviewStage Stage, DateTimeOffset? NextReviewAt,
    int CorrectCount, int WrongCount, TakeQuestionDto Question);

/// <param name="TotalDue">
/// Tổng số mục ĐẾN HẠN, có thể lớn hơn số phần tử trong <paramref name="Items"/> vì danh sách
/// bị cắt bớt. Không trả con số này thì giao diện hiện 20 mục và sinh viên tưởng đã ôn hết,
/// trong khi còn 60 mục nữa đang chờ.
/// </param>
public sealed record TodayReviewDto(
    int TotalDue, int TotalPending, int MasteredCount, List<ReviewItemDto> Items);

/// <summary>
/// Kết quả một lần trả lời mục ôn tập. Trả kèm đáp án đúng và giải thích: ôn tập không tính
/// điểm, và giấu đáp án ở đây là biến một bài học thành một câu đố vô ích.
/// </summary>
public sealed record ReviewAnswerDto(
    Guid ItemId, bool IsCorrect, ReviewStage Stage, DateTimeOffset? NextReviewAt, bool IsMastered,
    string? Explanation, List<Guid> CorrectOptionIds, List<string> AcceptedAnswers);

/// <param name="SourceChapterTitle">
/// Chương mà bộ câu ôn lấy nhãn kiến thức từ đó. null khi bài học nằm ở chương ĐẦU TIÊN —
/// không có chương trước để ôn, và đó là trạng thái bình thường chứ không phải lỗi.
/// </param>
public sealed record RecapDto(
    Guid ComponentId, string? SourceChapterTitle, List<string> FoundationTags,
    List<TakeQuestionDto> Questions);

/// <param name="AlreadyGenerated">
/// true = lượt làm này đã được nạp vào hàng đợi từ trước, lần gọi này không làm gì cả.
/// Job quét hằng ngày dựa vào cờ này để phân biệt "chưa nạp" với "nạp rồi mà không có gì mới".
/// </param>
public sealed record GenerateReviewResultDto(int ItemsCreated, int ItemsReset, bool AlreadyGenerated);

public static class ReviewQuestionMapper
{
    /// <summary>Quy đổi câu hỏi sang DTO phía người học — KHÔNG kèm đáp án đúng.</summary>
    public static TakeQuestionDto ToTakeDto(QuizQuestion q) => new(
        q.Id, q.Type, q.Text, q.Points,
        [.. q.Options.OrderBy(o => o.OrderIndex).Select(o => new TakeOptionDto(o.Id, o.Text))]);
}
