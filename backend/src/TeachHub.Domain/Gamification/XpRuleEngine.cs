namespace TeachHub.Domain.Gamification;

/// <summary>
/// Loại sự kiện được thưởng XP. Enum này là hợp đồng giữa bảng quy tắc (dữ liệu) và nơi phát
/// hiện sự kiện (handler) — THÊM giá trị vào CUỐI, không bao giờ chèn vào giữa: xuống DB nó
/// lưu bằng CHUỖI nên chèn giữa không làm hỏng dữ liệu, nhưng chèn giữa vẫn làm mọi bảng liệt
/// kê theo thứ tự khai báo đổi nghĩa trên giao diện.
/// </summary>
public enum XpEventType
{
    LessonCompleted,
    ComponentCompleted,
    AssignmentSubmittedOnTime,
    QuizHighScore,
    StreakMilestone,
}

/// <summary>
/// MỘT dòng trong bảng quy tắc XP — là DỮ LIỆU, không phải mã. Giảng viên sửa được số điểm và
/// ngưỡng giữa kỳ mà không cần deploy; đó là toàn bộ lý do engine không được chứa con số nào.
/// </summary>
public sealed class XpRuleSpec
{
    public required XpEventType EventType { get; set; }

    /// <summary>Số XP cộng khi quy tắc này áp dụng. Luôn dương — không có XP âm ở MVP.</summary>
    public required int Points { get; set; }

    /// <summary>
    /// Ngưỡng tối thiểu của <see cref="XpEventInput.Value"/> để quy tắc có hiệu lực, hiểu là
    /// "từ … trở lên" (>=). null = không xét giá trị. Ví dụ: quiz từ 8.0 điểm trở lên.
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>Số lần tối đa được cộng XP cho loại sự kiện này trong MỘT ngày. null = không trần.</summary>
    public int? DailyCap { get; set; }

    /// <summary>
    /// Tắt quy tắc phải có hiệu lực ngay giữa kỳ mà KHÔNG cần xóa nó: xóa là mất luôn lời giải
    /// thích vì sao sinh viên từng được cộng theo quy tắc đó.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <param name="Value">
/// Giá trị đi kèm sự kiện để so với ngưỡng — điểm quiz đã chuẩn hóa thang 10, số ngày streak…
/// Luôn <c>decimal</c>, không bao giờ <c>double</c>: mọi con số so với ngưỡng điểm trong hệ
/// thống này đều là decimal, trộn kiểu là mở đường cho 7.9999999 không qua nổi ngưỡng 8.
/// </param>
/// <param name="AwardedTodayForType">
/// Số lần loại sự kiện này ĐÃ được cộng XP trong ngày địa phương hiện tại. Người gọi đếm và
/// truyền vào — engine thuần, không truy vấn.
/// </param>
public sealed record XpEventInput(XpEventType Type, decimal? Value = null, int AwardedTodayForType = 0);

public sealed record XpAward(int Points, XpRuleSpec? Rule, string? SkipReason)
{
    public static XpAward None(string? reason = null) => new(0, null, reason);

    public bool IsAwarded => Points > 0;
}

/// <summary>
/// Quy đổi (sự kiện + bảng quy tắc) → số XP. Hàm thuần, không I/O, không đọc đồng hồ.
/// </summary>
public static class XpRuleEngine
{
    /// <summary>
    /// Bộ GIEO cho một lớp mới — vẫn là dữ liệu, ghi xuống bảng <c>xp_rules</c> rồi sửa được.
    /// Nó tồn tại chỉ để ngày đầu mở lớp không phải là một bảng trống mà giảng viên chưa biết
    /// điền gì. KHÔNG hàm nào khác trong engine đọc tới nó.
    /// </summary>
    public static IReadOnlyList<XpRuleSpec> SeedRules() =>
    [
        new() { EventType = XpEventType.LessonCompleted, Points = 10, DailyCap = 10 },
        new() { EventType = XpEventType.ComponentCompleted, Points = 2, DailyCap = 20 },
        new() { EventType = XpEventType.AssignmentSubmittedOnTime, Points = 25 },
        new() { EventType = XpEventType.QuizHighScore, Points = 20, MinValue = 8m },
        new() { EventType = XpEventType.QuizHighScore, Points = 30, MinValue = 9m },
        new() { EventType = XpEventType.StreakMilestone, Points = 15, MinValue = 7m },
    ];

    public static XpAward Evaluate(XpEventInput input, IReadOnlyList<XpRuleSpec> rules)
    {
        // Bậc ngưỡng CAO NHẤT thỏa mãn thắng thế; KHÔNG cộng dồn các bậc. Quiz 9.5 điểm với
        // bảng {≥8 → 20, ≥9 → 30} phải nhận đúng 30, không phải 50 — cộng dồn là cách một
        // bảng hai bậc âm thầm biến thành bảng ba bậc mà không ai khai ra.
        //
        // ThenByDescending(Points) rồi ThenBy(EventType) làm kết quả TẤT ĐỊNH khi có hai dòng
        // trùng ngưỡng (giảng viên bấm lưu hai lần): không có nó thì cùng một sinh viên đọc
        // hai lần ra hai con số khác nhau tùy thứ tự truy vấn trả về.
        var apDung = rules
            .Where(r => r.IsEnabled && r.EventType == input.Type)
            .Where(r => r.MinValue is not { } nguong || (input.Value is { } v && v >= nguong))
            .OrderByDescending(r => r.MinValue ?? decimal.MinValue)
            .ThenByDescending(r => r.Points)
            .FirstOrDefault();

        if (apDung is null) return XpAward.None();

        if (apDung.DailyCap is { } tran && input.AwardedTodayForType >= tran)
            return XpAward.None($"Đã đạt trần {tran} lần cộng XP trong ngày cho loại sự kiện này.");

        return new XpAward(apDung.Points, apDung, null);
    }
}
