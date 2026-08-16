namespace TeachHub.Domain.Grading;

/// <param name="RawScore">Điểm giảng viên chấm, TRƯỚC mọi mức phạt.</param>
/// <param name="FinalScore">Điểm hiệu lực, sau gợi ý và sau nộp trễ. Luôn nằm trong [0, RawScore].</param>
public sealed record ScoreBreakdown(
    decimal RawScore,
    int DaysLate,
    decimal LatePenaltyPercent,
    int HintsUnlocked,
    decimal HintPenaltyPercent,
    decimal FinalScore)
{
    public decimal PenaltyPoints =>
        Math.Round(RawScore - FinalScore, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Gộp hai mức phạt của Phase 6b thành một con số điểm hiệu lực, kèm bảng phân rã để giải
/// thích cho sinh viên "10 điểm − 20% gợi ý − 10% trễ = 7.2".
///
/// LUÔN tính từ điểm GỐC, không bao giờ tính chồng lên điểm đã phạt. Đây là điều kiện để hàm
/// này chạy lại được nhiều lần mà không đổi kết quả (idempotent) — thứ bắt buộc phải có, vì
/// giảng viên sẽ bấm "tính lại" sau khi sửa hạn nộp hoặc sửa mức phạt. Tính chồng thì mỗi lần
/// bấm là điểm tụt thêm một nấc, âm thầm, không có gì báo.
///
/// Hai mức phạt nhân với nhau (chứ không cộng tỉ lệ rồi trừ một lần) nên thứ tự áp không đổi
/// kết quả — nhờ vậy không có "thứ tự đúng" nào để nhớ sai.
/// </summary>
public static class SubmissionScoring
{
    public static ScoreBreakdown Apply(
        decimal rawScore,
        decimal latePenaltyPercentPerDay,
        DateTimeOffset? dueAt,
        DateTimeOffset? submittedAt,
        IEnumerable<decimal> hintPenaltyPercents)
    {
        var goiY = HintPenaltyCalculator.Apply(rawScore, hintPenaltyPercents);

        var soNgay = LatePenaltyCalculator.DaysLate(dueAt, submittedAt);
        var tyLeTre = Math.Clamp(latePenaltyPercentPerDay * soNgay, 0m, 100m);

        // Làm tròn MỘT LẦN ở cuối. Làm tròn sau mỗi bước là làm tròn hai lần trên cùng một
        // con số, và sai lệch tích lũy đúng ở chữ số thập phân thứ hai — chỗ sinh viên cộng
        // tay lại và phát hiện ra.
        var cuoi = Math.Round(
            Math.Max(0m, rawScore * (1m - goiY.PercentDeducted / 100m) * (1m - tyLeTre / 100m)),
            2, MidpointRounding.AwayFromZero);

        return new ScoreBreakdown(
            rawScore, soNgay, tyLeTre, goiY.HintsUnlocked, goiY.PercentDeducted, cuoi);
    }
}
