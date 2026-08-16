namespace TeachHub.Domain.Grading;

/// <param name="PercentDeducted">
/// Tỉ lệ THẬT SỰ bị trừ, đã kẹp ở 100%. Không báo cáo con số thô 150%: nó gợi ý rằng điểm có
/// thể xuống âm, trong khi hệ thống không bao giờ để chuyện đó xảy ra.
/// </param>
public sealed record LatePenalty(
    int DaysLate, decimal PercentDeducted, decimal PenaltyPoints, decimal FinalScore);

/// <summary>
/// Phạt nộp trễ theo <c>Assignment.LatePenaltyPercentPerDay</c>. Hàm thuần: nhận mốc thời gian
/// từ ngoài (TimeProvider của tầng gọi) chứ không đọc <c>DateTimeOffset.UtcNow</c> — nếu không
/// thì kiểm một ca "trễ 3 ngày" phải chờ ba ngày thật trôi qua.
/// </summary>
public static class LatePenaltyCalculator
{
    /// <summary>
    /// Số ngày trễ, LÀM TRÒN LÊN — trễ một phút cũng là trễ một ngày. Làm tròn xuống thì cả
    /// lớp nộp lúc 00:05 và không ai bị phạt gì, tức là hạn nộp không còn ý nghĩa.
    /// </summary>
    public static int DaysLate(DateTimeOffset? dueAt, DateTimeOffset? submittedAt)
    {
        if (dueAt is not { } han || submittedAt is not { } nop) return 0;

        var tre = nop - han;
        if (tre <= TimeSpan.Zero) return 0;

        // Tính bằng SỐ NGUYÊN ticks, không dùng Math.Ceiling(tre.TotalDays).
        // TotalDays là double: 24 giờ chẵn có thể ra 1.0000000000000002 tùy phép trừ, và lúc
        // đó sinh viên nộp đúng 24 giờ sau hạn bị phạt gấp đôi người nộp sau đó một giây.
        var (nguyen, du) = Math.DivRem(tre.Ticks, TimeSpan.TicksPerDay);

        return (int)(nguyen + (du > 0 ? 1 : 0));
    }

    public static LatePenalty Apply(
        decimal score, decimal percentPerDay, DateTimeOffset? dueAt, DateTimeOffset? submittedAt)
    {
        var soNgay = DaysLate(dueAt, submittedAt);

        // Kẹp ở [0, 100] LÀ chỗ giữ cho điểm không bao giờ âm. 30%/ngày × 5 ngày = 150%, và
        // một cột điểm âm sẽ kéo điểm tổng kết xuống dưới cả sinh viên không nộp gì cả.
        var tyLe = Math.Clamp(percentPerDay * soNgay, 0m, 100m);

        var cuoi = Math.Round(
            Math.Max(0m, score * (1m - tyLe / 100m)), 2, MidpointRounding.AwayFromZero);

        return new LatePenalty(
            soNgay, tyLe, Math.Round(score - cuoi, 2, MidpointRounding.AwayFromZero), cuoi);
    }
}
