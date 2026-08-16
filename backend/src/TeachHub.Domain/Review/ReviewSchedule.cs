namespace TeachHub.Domain.Review;

/// <summary>
/// Bậc trên thang ôn tập ngắt quãng. Tên bậc chính là khoảng cách tới lần ôn kế tiếp, nên đọc
/// một hàng trong DB là hiểu ngay lịch của nó — lưu xuống database dưới dạng CHUỖI.
/// </summary>
public enum ReviewStage { ThreeDays, OneWeek, TwoWeeks, Mastered }

/// <summary>
/// Trạng thái của một mục trong hàng đợi. Tách khỏi <see cref="ReviewStage"/> vì truy vấn
/// "Ôn tập hôm nay" và job quét hằng ngày đều lọc theo trạng thái, và partial index
/// <c>WHERE status = 'Pending'</c> rẻ hơn hẳn so với lọc theo bậc.
/// </summary>
public enum ReviewItemStatus { Pending, Mastered }

/// <param name="NextReviewAt">
/// null nghĩa là KHÔNG còn lần ôn nào nữa (đã thuộc). Cố ý nullable thay vì để lại mốc cũ:
/// một mốc cũ còn sót lại trên mục đã thuộc là mục đó nằm mãi trong "Ôn tập hôm nay", và
/// sinh viên sẽ ngừng tin vào cả hàng đợi.
/// </param>
public readonly record struct ReviewScheduleResult(ReviewStage Stage, DateTimeOffset? NextReviewAt)
{
    public bool IsMastered => Stage == ReviewStage.Mastered;
}

/// <summary>
/// Toàn bộ luật lên lịch ôn tập — hàm thuần, không I/O, không đọc đồng hồ. Mốc thời gian luôn
/// truyền vào qua tham số <c>now</c> (ở tầng ứng dụng là <c>TimeProvider</c>).
///
/// Không có hàm nào ở đây được phép gọi <c>DateTimeOffset.UtcNow</c>: kiểm một mục đi hết
/// thang bằng đồng hồ thật là chờ 24 ngày, nên luật sẽ không bao giờ được test và ca "sai ở
/// bậc cuối" sẽ đi thẳng ra môi trường thật.
/// </summary>
public static class ReviewSchedule
{
    /// <summary>Thang cố định của Phase 8: 3 ngày → 1 tuần → 2 tuần → thuộc.</summary>
    public static readonly TimeSpan FirstInterval = TimeSpan.FromDays(3);

    /// <summary>Khoảng chờ ứng với một bậc; null ở bậc đã thuộc vì không còn lần ôn nào.</summary>
    public static TimeSpan? IntervalFor(ReviewStage stage) => stage switch
    {
        ReviewStage.ThreeDays => TimeSpan.FromDays(3),
        ReviewStage.OneWeek => TimeSpan.FromDays(7),
        ReviewStage.TwoWeeks => TimeSpan.FromDays(14),
        _ => null,
    };

    /// <summary>
    /// Lịch cho một mục vừa sinh ra từ một câu trả lời SAI, và cũng là điểm quay về khi trả
    /// lời sai ở bất kỳ bậc nào.
    /// </summary>
    public static ReviewScheduleResult Start(DateTimeOffset now) =>
        new(ReviewStage.ThreeDays, now + FirstInterval);

    /// <summary>
    /// Lịch kế tiếp sau khi sinh viên trả lời một mục đang ở bậc <paramref name="stage"/>.
    ///
    /// Mốc kế tiếp tính từ <paramref name="now"/> — thời điểm TRẢ LỜI — chứ không từ hạn cũ.
    /// Tính từ hạn cũ thì sinh viên bỏ quên hàng đợi mười ngày rồi mới ôn sẽ bị ép ôn lại chỉ
    /// vài hôm sau, tức là hệ thống dồn ép đúng người đang học chậm nhất.
    /// </summary>
    public static ReviewScheduleResult Next(ReviewStage stage, bool traLoiDung, DateTimeOffset now)
    {
        // Sai là quay về đầu chu kỳ, KHÔNG lùi một bậc. Lùi một bậc nghe "công bằng" hơn
        // nhưng giữ nguyên giả định rằng sinh viên vẫn nhớ phần đã ôn — trong khi bằng chứng
        // vừa có là họ không nhớ. Áp cho cả mục đã thuộc: khái niệm nền tảng sai lại ở môn
        // sau phải được học lại từ đầu (Gap #2 — hàng đợi gắn với CON NGƯỜI, không gắn lớp).
        if (!traLoiDung) return Start(now);

        var bacMoi = stage switch
        {
            ReviewStage.ThreeDays => ReviewStage.OneWeek,
            ReviewStage.OneWeek => ReviewStage.TwoWeeks,
            _ => ReviewStage.Mastered,        // TwoWeeks trả lời đúng, và Mastered giữ nguyên
        };

        return new ReviewScheduleResult(
            bacMoi,
            IntervalFor(bacMoi) is { } khoang ? now + khoang : null);
    }
}
