using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

public sealed record XpRuleDto(
    Guid Id, XpEventType EventType, int Points, decimal? MinValue, int? DailyCap, bool IsEnabled)
{
    public static XpRuleDto From(XpRule r) =>
        new(r.Id, r.EventType, r.Points, r.MinValue, r.DailyCap, r.IsEnabled);
}

public sealed record BadgeConditionDto(BadgeMetric Metric, BadgeComparison Comparison, decimal Threshold);

public sealed record BadgeDto(
    Guid Id, string Code, string Name, string? Description, string? Icon,
    BadgeLogic Logic, bool IsEnabled, List<BadgeConditionDto> Conditions)
{
    public static BadgeDto From(Badge b) =>
        new(b.Id, b.Code, b.Name, b.Description, b.Icon, b.Logic, b.IsEnabled,
            [.. b.Conditions.Select(c => new BadgeConditionDto(c.Metric, c.Comparison, c.Threshold))]);
}

public sealed record GamificationSettingsDto(
    Guid OfferingId, bool IsEnabled, LeaderboardVisibility LeaderboardVisibility, int LeaderboardSize)
{
    public static GamificationSettingsDto From(OfferingGamificationSettings s) =>
        new(s.OfferingId, s.IsEnabled, s.LeaderboardVisibility, s.LeaderboardSize);
}

public sealed record EarnedBadgeDto(
    Guid BadgeId, string Code, string Name, string? Description, string? Icon, DateTimeOffset EarnedAt);

/// <param name="Current">Giá trị hiện tại của chỉ số — để giao diện vẽ được thanh tiến độ, không chỉ nói "chưa đạt".</param>
public sealed record ConditionProgressDto(
    BadgeMetric Metric, BadgeComparison Comparison, decimal Threshold, decimal Current, bool Met);

public sealed record BadgeProgressDto(
    Guid BadgeId, string Code, string Name, string? Description, string? Icon,
    List<ConditionProgressDto> Conditions);

public sealed record XpByTypeDto(XpEventType Type, int Points, int Count);

/// <param name="LastActiveDate">
/// NGÀY LỊCH ĐỊA PHƯƠNG (yyyy-MM-dd) theo múi giờ của lớp, cố ý KHÔNG phải một mốc thời gian.
/// Trả về <c>DateTimeOffset</c> ở đây là mời trình duyệt quy đổi lại theo múi giờ của MÁY SINH VIÊN,
/// và một người mở trang khi đang ở nước ngoài sẽ thấy streak lệch một ngày so với bạn cùng lớp.
/// </param>
public sealed record MyGamificationDto(
    Guid OfferingId, bool IsEnabled, int TotalXp,
    int CurrentStreakDays, int LongestStreakDays, string? LastActiveDate,
    int Rank, int TotalParticipants,
    List<XpByTypeDto> XpByType, List<EarnedBadgeDto> Badges, List<BadgeProgressDto> UpcomingBadges);

/// <param name="DisplayName">
/// Ở chế độ ẩn danh đây là "Sinh viên #hạng" — và KHÔNG có trường nào khác trong hàng mang tên
/// thật hay MSSV. Gửi tên xuống rồi để giao diện tự ẩn là làm ẩn danh trên màn hình nhưng công
/// khai trong tab Network của trình duyệt.
/// </param>
public sealed record LeaderboardRowDto(
    int Rank, string DisplayName, int TotalXp, int StreakDays, int BadgeCount, bool IsMe);

public sealed record LeaderboardDto(
    Guid OfferingId, bool IsEnabled, LeaderboardVisibility Visibility,
    int TotalParticipants, int? MyRank, int MyXp, List<LeaderboardRowDto> Rows);

public sealed record RecalculationSummaryDto(
    Guid OfferingId, int EnrollmentsProcessed, int XpEventsCreated, int BadgesAwarded);
