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

/// <param name="Current">The metric's current value — so the UI can draw a progress bar instead of merely saying "not earned yet".</param>
public sealed record ConditionProgressDto(
    BadgeMetric Metric, BadgeComparison Comparison, decimal Threshold, decimal Current, bool Met);

public sealed record BadgeProgressDto(
    Guid BadgeId, string Code, string Name, string? Description, string? Icon,
    List<ConditionProgressDto> Conditions);

public sealed record XpByTypeDto(XpEventType Type, int Points, int Count);

/// <param name="LastActiveDate">
/// The LOCAL CALENDAR DATE (yyyy-MM-dd) in the offering's time zone, deliberately NOT an instant.
/// Returning a <c>DateTimeOffset</c> here invites the browser to re-convert it into the time zone of
/// the STUDENT'S MACHINE, and someone opening the page while abroad would see their streak off by a
/// day compared with their classmates.
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
