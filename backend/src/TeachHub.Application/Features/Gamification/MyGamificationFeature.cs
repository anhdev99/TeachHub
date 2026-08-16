using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

/// <summary>
/// Dựng trang tiến độ cá nhân. Dùng chung cho cả lệnh đồng bộ lẫn truy vấn đọc, nên hai đường
/// không thể hiện ra hai con số khác nhau cho cùng một sinh viên.
/// </summary>
internal static class MyGamificationProjection
{
    public static async Task<MyGamificationDto> BuildAsync(
        IAppDbContext db, LearnerContext learner, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var zone = StreakCalculator.ResolveZone(learner.TimeZoneId);

        var settings = await GamificationEngine.ReadSettingsAsync(db, learner.OfferingId, ct);
        var facts = await GamificationEngine.ReadFactsAsync(db, learner.EnrollmentId, ct);
        var rules = await GamificationEngine.ReadRulesAsync(db, learner.OfferingId, ct);

        // Streak tính lại từ hoạt động thật ở MỖI lần đọc, không đọc bản lưu sẵn: nó phụ thuộc
        // "hôm nay là ngày nào", nên một con số lưu cứng sẽ đúng lúc ghi và sai vào sáng hôm sau.
        var streak = StreakCalculator.Compute(facts.AllMoments, zone, now);

        var events = await db.XpEvents.AsNoTracking()
            .Where(e => e.EnrollmentId == learner.EnrollmentId)
            .ToListAsync(ct);

        var tongXp = events.Sum(e => e.Points);
        var chiSo = GamificationEngine.BuildMetrics(
            facts, [.. rules.Select(r => r.ToSpec())], tongXp, streak);

        var daTrao = await (from eb in db.EarnedBadges.AsNoTracking()
                            where eb.EnrollmentId == learner.EnrollmentId
                            join b in db.Badges on eb.BadgeId equals b.Id
                            orderby eb.EarnedAt
                            select new EarnedBadgeDto(
                                b.Id, b.Code, b.Name, b.Description, b.Icon, eb.EarnedAt))
                           .ToListAsync(ct);

        var daCoId = daTrao.Select(x => x.BadgeId).ToHashSet();

        var catalog = await db.Badges.AsNoTracking()
            .Where(b => b.InstructorId == learner.InstructorId && b.IsEnabled)
            .OrderBy(b => b.Code)
            .ToListAsync(ct);

        // Huy hiệu CHƯA đạt kèm giá trị hiện tại của từng điều kiện. Chỉ nói "chưa đạt" là một
        // màn hình không dùng được: sinh viên không biết còn thiếu bao nhiêu thì không có lý do
        // gì để cố thêm, mà đó chính là toàn bộ mục đích của huy hiệu.
        var sapDat = catalog
            .Where(b => !daCoId.Contains(b.Id) && b.Conditions.Count > 0)
            .Select(b => new BadgeProgressDto(b.Id, b.Code, b.Name, b.Description, b.Icon,
                [.. b.Conditions.Select(c => new ConditionProgressDto(
                    c.Metric, c.Comparison, c.Threshold, chiSo.ValueOf(c.Metric),
                    BadgeEvaluator.IsEarned(
                        new BadgeSpec(b.Id, b.Code, BadgeLogic.All, [c]), chiSo)))]))
            .ToList();

        var (hang, tongNguoi) = await RankAsync(db, learner, tongXp, ct);

        return new MyGamificationDto(
            learner.OfferingId, settings.IsEnabled, tongXp,
            streak.Current, streak.Longest,
            streak.LastActiveLocalDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            hang, tongNguoi,
            [.. events.GroupBy(e => e.Type)
                      .Select(g => new XpByTypeDto(g.Key, g.Sum(x => x.Points), g.Count()))
                      .OrderByDescending(x => x.Points)],
            daTrao, sapDat);
    }

    /// <summary>
    /// Hạng tính từ SỔ CÁI xp_events chứ không từ cột đệm <c>enrollments.xp_points</c>: sổ cái
    /// là nguồn chân lý, còn cột đệm chỉ đúng tới lần đồng bộ gần nhất của TỪNG người.
    /// </summary>
    private static async Task<(int Rank, int Total)> RankAsync(
        IAppDbContext db, LearnerContext learner, int myXp, CancellationToken ct)
    {
        var diem = await GamificationAccess.ActiveEnrollments(db, learner.OfferingId)
            .AsNoTracking()
            .Select(e => db.XpEvents.Where(x => x.EnrollmentId == e.Id).Sum(x => (int?)x.Points) ?? 0)
            .ToListAsync(ct);

        return (1 + diem.Count(x => x > myXp), diem.Count);
    }
}

// ── Đồng bộ tiến độ của chính mình ──────────────────────────────────────────────────────

/// <summary>
/// KHÔNG có endpoint nào cho phép "cấp XP". Lệnh này chỉ TÍNH LẠI từ dữ liệu học tập đã có,
/// nên kể cả khi sinh viên gọi nó liên tục cũng không tạo ra được một điểm XP nào mà hoạt động
/// của họ không biện minh được. Một endpoint nhận số XP từ client là một endpoint mà sinh viên
/// đầu tiên biết mở DevTools sẽ dùng để đứng đầu bảng xếp hạng.
/// </summary>
public sealed record SyncMyGamificationCommand(Guid OfferingId) : ICommand<Result<MyGamificationDto>>
{
    public Guid UserId { get; init; }
}

public sealed class SyncMyGamificationHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SyncMyGamificationCommand, Result<MyGamificationDto>>
{
    public async Task<Result<MyGamificationDto>> Handle(
        SyncMyGamificationCommand request, CancellationToken ct)
    {
        var learner = await GamificationAccess.LearnerAsync(db, request.OfferingId, request.UserId, ct);
        if (learner is null) return Result<MyGamificationDto>.NotFound("Không tìm thấy lớp học phần.");

        await GamificationEngine.RecalculateAsync(db, learner, clock, ct);
        await db.SaveChangesAsync(ct);

        return Result<MyGamificationDto>.Ok(
            await MyGamificationProjection.BuildAsync(db, learner, clock, ct));
    }
}

// ── Đọc tiến độ của chính mình ──────────────────────────────────────────────────────────

public sealed record GetMyGamificationQuery(Guid OfferingId, Guid UserId)
    : IQuery<Result<MyGamificationDto>>;

public sealed class GetMyGamificationHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetMyGamificationQuery, Result<MyGamificationDto>>
{
    public async Task<Result<MyGamificationDto>> Handle(
        GetMyGamificationQuery request, CancellationToken ct)
    {
        var learner = await GamificationAccess.LearnerAsync(db, request.OfferingId, request.UserId, ct);
        if (learner is null) return Result<MyGamificationDto>.NotFound("Không tìm thấy lớp học phần.");

        return Result<MyGamificationDto>.Ok(
            await MyGamificationProjection.BuildAsync(db, learner, clock, ct));
    }
}

// ── Giảng viên tính lại cho cả lớp ──────────────────────────────────────────────────────

/// <summary>
/// Cần thiết vì XP chỉ được ghi khi có ai đó tính lại: sinh viên chưa bao giờ mở trang tiến độ
/// thì bảng xếp hạng thiếu họ. Một lần bấm sau khi đổi bảng quy tắc là bảng xếp hạng đúng lại.
/// </summary>
public sealed record RecalculateOfferingGamificationCommand(Guid OfferingId)
    : ICommand<Result<RecalculationSummaryDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class RecalculateOfferingGamificationHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<RecalculateOfferingGamificationCommand, Result<RecalculationSummaryDto>>
{
    public async Task<Result<RecalculationSummaryDto>> Handle(
        RecalculateOfferingGamificationCommand request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<RecalculationSummaryDto>.NotFound("Không tìm thấy lớp học phần.");

        var enrollmentIds = await GamificationAccess.ActiveEnrollments(db, request.OfferingId)
            .AsNoTracking().Select(e => e.Id).ToListAsync(ct);

        var soSuKien = 0;
        var soHuyHieu = 0;

        foreach (var id in enrollmentIds)
        {
            var learner = new LearnerContext(id, lop.OfferingId, lop.InstructorId, lop.TimeZoneId);

            var snapshot = await GamificationEngine.RecalculateAsync(db, learner, clock, ct);

            // Lưu theo TỪNG sinh viên: một lớp 300 người gom vào một SaveChanges là một câu lệnh
            // khổng lồ, và một hàng hỏng làm hỏng cả lượt. TransactionBehavior vẫn bọc toàn bộ
            // lệnh này trong MỘT transaction nên tính nguyên tử không mất.
            await db.SaveChangesAsync(ct);

            soSuKien += snapshot.NewEvents.Count;
            soHuyHieu += snapshot.NewBadgeCount;
        }

        return Result<RecalculationSummaryDto>.Ok(new RecalculationSummaryDto(
            request.OfferingId, enrollmentIds.Count, soSuKien, soHuyHieu));
    }
}
