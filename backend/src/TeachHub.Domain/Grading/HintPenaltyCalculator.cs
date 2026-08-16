namespace TeachHub.Domain.Grading;

public sealed record HintPenalty(
    int HintsUnlocked, decimal PercentDeducted, decimal PenaltyPoints, decimal FinalScore);

/// <summary>
/// Trừ điểm theo số gợi ý sinh viên đã mở (Gap #11, SRS UC-09). Hàm thuần.
///
/// Nhận danh sách TỈ LỆ ĐÃ CHỐT LÚC MỞ (<c>HintUnlock.PenaltyPercentApplied</c>) chứ không
/// nhận id gợi ý rồi tự đi đọc mức phạt hiện tại: giảng viên sửa mức phạt sau khi nửa lớp đã
/// mở gợi ý thì những sinh viên đó phải giữ đúng điều kiện họ đã thấy trên màn hình lúc bấm.
/// </summary>
public static class HintPenaltyCalculator
{
    public static HintPenalty Apply(decimal score, IEnumerable<decimal> unlockedPenaltyPercents)
    {
        var ds = unlockedPenaltyPercents.ToList();

        // Cộng dồn rồi kẹp ở 100%: mở bốn gợi ý 30% là 120%, và không kẹp thì điểm ra âm.
        var tyLe = Math.Clamp(ds.Sum(), 0m, 100m);

        var cuoi = Math.Round(
            Math.Max(0m, score * (1m - tyLe / 100m)), 2, MidpointRounding.AwayFromZero);

        return new HintPenalty(
            ds.Count, tyLe, Math.Round(score - cuoi, 2, MidpointRounding.AwayFromZero), cuoi);
    }
}
