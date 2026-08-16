using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Gamification;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Gamification;

public sealed record ActivityFact(Guid SourceId, DateTimeOffset At);

public sealed record SubmissionFact(Guid SourceId, DateTimeOffset At, bool IsLate);

public sealed record QuizFact(Guid SourceId, DateTimeOffset At, decimal Score);

/// <summary>
/// Những gì sinh viên ĐÃ THẬT SỰ LÀM, đọc thẳng từ dữ liệu học tập bền vững. XP và huy hiệu
/// đều suy ra từ đây chứ không bao giờ do người gọi khẳng định — nhờ vậy không có endpoint nào
/// "cấp XP" để sinh viên gọi thẳng, và chạy lại phép tính bao nhiêu lần cũng ra cùng kết quả.
/// </summary>
public sealed record ActivityFacts(
    IReadOnlyList<ActivityFact> Lessons,
    IReadOnlyList<ActivityFact> Components,
    IReadOnlyList<SubmissionFact> Submissions,
    IReadOnlyList<QuizFact> Quizzes)
{
    public static ActivityFacts Empty { get; } = new([], [], [], []);

    /// <summary>
    /// Mọi mốc hoạt động — nguồn DUY NHẤT của streak.
    ///
    /// Cố ý KHÔNG lấy từ bảng <c>xp_events</c>: một bài học hoàn thành sau khi đã chạm trần XP
    /// trong ngày vẫn là một ngày có học. Lấy theo sự kiện XP thì trần XP âm thầm ăn mất ngày
    /// streak của người học chăm nhất — đúng nhóm mà cơ chế này phục vụ.
    /// </summary>
    public IEnumerable<DateTimeOffset> AllMoments =>
        Lessons.Select(x => x.At)
            .Concat(Components.Select(x => x.At))
            .Concat(Submissions.Select(x => x.At))
            .Concat(Quizzes.Select(x => x.At));
}

/// <param name="NewEvents">
/// CHỈ những sự kiện vừa được tạo trong lượt tính này, không gồm sự kiện đã có. Trộn hai thứ
/// làm báo cáo "đã cộng bao nhiêu XP mới" nhân lên theo số lần bấm tính lại, và giảng viên
/// tưởng cơ chế đang phát XP loạn xạ.
/// </param>
public sealed record LearnerSnapshot(
    int TotalXp, StreakOutcome Streak, LearnerMetrics Metrics,
    IReadOnlyList<XpEvent> NewEvents, int NewBadgeCount);

/// <summary>
/// Lớp tĩnh, KHÔNG phải service đăng ký DI — cùng khuôn với <see cref="Learning.LearningAccess"/>.
/// Handler truyền <c>IAppDbContext</c> và <c>TimeProvider</c> đã tiêm sẵn của nó vào.
/// </summary>
public static class GamificationEngine
{
    public static async Task<ActivityFacts> ReadFactsAsync(
        IAppDbContext db, Guid enrollmentId, CancellationToken ct)
    {
        var baiHoc = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId
                        && p.Status == ProgressStatus.Completed
                        && p.CompletedAt != null)
            .Select(p => new ActivityFact(p.LessonId, p.CompletedAt!.Value))
            .ToListAsync(ct);

        var phanBai = await db.ComponentProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId
                        && p.Status == ProgressStatus.Completed
                        && p.CompletedAt != null)
            .Select(p => new ActivityFact(p.LessonComponentId, p.CompletedAt!.Value))
            .ToListAsync(ct);

        // Chỉ bản nộp HIỆN HÀNH: nộp lại tạo hàng mới với is_current = false (Gap #5), đếm cả
        // bản cũ là thưởng XP nhiều lần cho đúng một bài.
        //
        // GIỚI HẠN ĐÃ BIẾT: bài nộp theo NHÓM có enrollment_id null (chỉ có group_id) nên chưa
        // vào đây. Ghi nhận XP cho từng thành viên nhóm cần đi qua group_members và là việc của
        // vòng sau — đừng lặng lẽ "sửa" bằng cách bỏ điều kiện enrollment_id.
        var baiNop = await db.Submissions.AsNoTracking()
            .Where(s => s.EnrollmentId == enrollmentId && s.IsCurrent && s.SubmittedAt != null)
            .OrderBy(s => s.SubmittedAt)
            .Select(s => new SubmissionFact(s.Id, s.SubmittedAt!.Value, s.IsLate))
            .ToListAsync(ct);

        // NormalizedScore là điểm đã quy về thang 10 — đúng thang mà ngưỡng trong bảng quy tắc
        // dùng. Lấy RawScore thì một quiz 20 câu cho ra 18 điểm và mọi ngưỡng đều vô nghĩa.
        var quiz = await db.QuizAttempts.AsNoTracking()
            .Where(a => a.EnrollmentId == enrollmentId
                        && a.SubmittedAt != null
                        && a.NormalizedScore != null)
            .Select(a => new QuizFact(a.Id, a.SubmittedAt!.Value, a.NormalizedScore!.Value))
            .ToListAsync(ct);

        return new ActivityFacts(baiHoc, phanBai, baiNop, quiz);
    }

    public static Task<List<XpRule>> ReadRulesAsync(
        IAppDbContext db, Guid offeringId, CancellationToken ct) =>
        db.XpRules.AsNoTracking().Where(r => r.OfferingId == offeringId).ToListAsync(ct);

    /// <summary>Cấu hình của lớp, hoặc bản mặc định (ẩn danh, đang bật) nếu chưa ai cấu hình.</summary>
    public static async Task<OfferingGamificationSettings> ReadSettingsAsync(
        IAppDbContext db, Guid offeringId, CancellationToken ct) =>
        await db.OfferingGamificationSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.OfferingId == offeringId, ct)
        ?? OfferingGamificationSettings.DefaultFor(offeringId);

    public static LearnerMetrics BuildMetrics(
        ActivityFacts facts, IReadOnlyList<XpRuleSpec> rules, int totalXp, StreakOutcome streak) =>
        new()
        {
            TotalXp = totalXp,
            CurrentStreakDays = streak.Current,
            LongestStreakDays = streak.Longest,
            LessonsCompleted = facts.Lessons.Count,
            OnTimeSubmissions = facts.Submissions.Count(s => !s.IsLate),
            LateSubmissions = facts.Submissions.Count(s => s.IsLate),

            // "Liên tiếp" tính tới lần nộp GẦN NHẤT, nên thứ tự thời gian là bắt buộc.
            ConsecutiveOnTimeSubmissions = BadgeEvaluator.CurrentOnTimeRun(
                facts.Submissions.OrderBy(s => s.At).Select(s => !s.IsLate)),

            // "Quiz điểm cao" là quiz mà BẢNG QUY TẮC coi là cao — không có ngưỡng nào hard-code
            // ở đây. Đổi ngưỡng trong bảng là đổi luôn ý nghĩa của huy hiệu, đúng như mong đợi.
            HighScoreQuizzes = facts.Quizzes.Count(q =>
                XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, q.Score), rules).IsAwarded),
        };

    /// <summary>
    /// Tính lại toàn bộ XP và huy hiệu của MỘT enrollment từ dữ liệu học tập, rồi ghi phần còn
    /// thiếu. KHÔNG gọi <c>SaveChangesAsync</c> — người gọi quyết định thời điểm lưu, và
    /// TransactionBehavior đã bọc sẵn mọi ICommand nên không handler nào tự mở transaction.
    ///
    /// Idempotent theo thiết kế: mỗi sự kiện có một <c>DedupeKey</c> và unique index
    /// <c>(enrollment_id, dedupe_key)</c> canh giữ. Chạy mười lần cũng ra đúng một lần cộng.
    /// </summary>
    public static async Task<LearnerSnapshot> RecalculateAsync(
        IAppDbContext db, LearnerContext learner, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var zone = StreakCalculator.ResolveZone(learner.TimeZoneId);

        var rules = await ReadRulesAsync(db, learner.OfferingId, ct);
        var specs = rules.Select(r => r.ToSpec()).ToList();
        var facts = await ReadFactsAsync(db, learner.EnrollmentId, ct);

        var daCo = await db.XpEvents.AsNoTracking()
            .Where(e => e.EnrollmentId == learner.EnrollmentId)
            .ToListAsync(ct);

        var khoaDaCo = daCo.Select(e => e.DedupeKey).ToHashSet(StringComparer.Ordinal);

        // Đếm số lần đã cộng theo (loại sự kiện, NGÀY ĐỊA PHƯƠNG) — trần mỗi ngày phải giữ
        // nguyên hiệu lực qua nhiều lần đồng bộ, nếu không thì bấm đồng bộ hai lần là lách trần.
        var demTrongNgay = daCo
            .GroupBy(e => (e.Type, StreakCalculator.LocalDateOf(e.OccurredAt, zone)))
            .ToDictionary(g => g.Key, g => g.Count());

        var suKienMoi = new List<XpEvent>();

        // Thứ tự thời gian là BẮT BUỘC: trần mỗi ngày cắt từ sự kiện muộn nhất, nên xử lý sai
        // thứ tự thì cùng một tập hoạt động cho ra hai tổng XP khác nhau. ThenBy(Key) để hai
        // sự kiện cùng mốc thời gian vẫn có thứ tự tất định.
        foreach (var ungVien in BuildCandidates(facts).OrderBy(c => c.At).ThenBy(c => c.Key, StringComparer.Ordinal))
        {
            if (!khoaDaCo.Add(ungVien.Key)) continue;   // đã cộng rồi — bỏ qua, không cộng lại

            var ngay = StreakCalculator.LocalDateOf(ungVien.At, zone);
            var daCongHomDo = demTrongNgay.GetValueOrDefault((ungVien.Type, ngay));

            var thuong = XpRuleEngine.Evaluate(
                new XpEventInput(ungVien.Type, ungVien.Value, daCongHomDo), specs);

            if (!thuong.IsAwarded) continue;

            demTrongNgay[(ungVien.Type, ngay)] = daCongHomDo + 1;

            // Add tường minh qua DbSet. Entity.Id tự sinh UUIDv7 lúc khởi tạo, nên thêm con qua
            // navigation của một parent đang tracked làm EF phát UPDATE thay vì INSERT.
            var e = new XpEvent
            {
                EnrollmentId = learner.EnrollmentId,
                Type = ungVien.Type,
                Points = thuong.Points,
                DedupeKey = ungVien.Key,
                OccurredAt = ungVien.At,
                SourceId = ungVien.SourceId,
            };

            db.XpEvents.Add(e);
            suKienMoi.Add(e);
        }

        var streak = StreakCalculator.Compute(facts.AllMoments, zone, now);

        // Mốc streak xử lý riêng, KHÔNG qua XpRuleEngine.Evaluate: engine cố ý chỉ chọn bậc cao
        // nhất (quiz 9.5 nhận 30 chứ không phải 20+30), còn mốc streak thì mỗi mốc đạt được
        // đáng một lần thưởng riêng — 7 ngày rồi 30 ngày là hai thành tựu khác nhau.
        foreach (var quy in rules.Where(r => r.IsEnabled
                                             && r.EventType == XpEventType.StreakMilestone
                                             && r.MinValue is not null))
        {
            var moc = quy.MinValue!.Value;
            if (streak.Longest < moc) continue;

            var khoa = $"streak:{moc.ToString("0.##", CultureInfo.InvariantCulture)}";
            if (!khoaDaCo.Add(khoa)) continue;

            // OccurredAt = now là an toàn ĐÚNG VÌ streak đọc từ ActivityFacts chứ không từ
            // xp_events: nếu streak đọc từ sự kiện XP thì mốc thưởng ghi hôm nay sẽ tự đánh dấu
            // hôm nay là ngày có học và thổi phồng chính chuỗi vừa thưởng.
            var e = new XpEvent
            {
                EnrollmentId = learner.EnrollmentId,
                Type = XpEventType.StreakMilestone,
                Points = quy.Points,
                DedupeKey = khoa,
                OccurredAt = now,
            };

            db.XpEvents.Add(e);
            suKienMoi.Add(e);
        }

        var tongXp = daCo.Sum(e => e.Points) + suKienMoi.Sum(e => e.Points);
        var chiSo = BuildMetrics(facts, specs, tongXp, streak);

        var soHuyHieuMoi = await AwardBadgesAsync(db, learner, chiSo, now, ct);

        // Hai cột này đã được Phase 2 giữ chỗ sẵn trên Enrollment. Chúng là BẢN SAO TIỆN DỤNG
        // cho bảng xếp hạng và trang tổng quan; sổ cái xp_events mới là nguồn chân lý, và mỗi
        // lần tính lại đều ghi đè bằng tổng của sổ cái nên không có đường phân kỳ lâu dài.
        var enrollment = await db.Enrollments.SingleAsync(e => e.Id == learner.EnrollmentId, ct);
        enrollment.XpPoints = tongXp;
        enrollment.StreakDays = streak.Current;

        return new LearnerSnapshot(tongXp, streak, chiSo, suKienMoi, soHuyHieuMoi);
    }

    private static async Task<int> AwardBadgesAsync(
        IAppDbContext db, LearnerContext learner, LearnerMetrics metrics,
        DateTimeOffset now, CancellationToken ct)
    {
        var catalog = await db.Badges.AsNoTracking()
            .Where(b => b.InstructorId == learner.InstructorId && b.IsEnabled)
            .ToListAsync(ct);

        if (catalog.Count == 0) return 0;

        var daTrao = await db.EarnedBadges.AsNoTracking()
            .Where(x => x.EnrollmentId == learner.EnrollmentId)
            .Select(x => x.BadgeId)
            .ToListAsync(ct);

        var moi = BadgeEvaluator.NewlyEarned(
            catalog.Select(b => b.ToSpec()), metrics, daTrao.ToHashSet());

        foreach (var b in moi)
            db.EarnedBadges.Add(new EarnedBadge
            {
                BadgeId = b.Id, EnrollmentId = learner.EnrollmentId, EarnedAt = now,
            });

        return moi.Count;
    }

    private sealed record Candidate(
        XpEventType Type, string Key, DateTimeOffset At, decimal? Value, Guid? SourceId);

    private static IEnumerable<Candidate> BuildCandidates(ActivityFacts facts)
    {
        foreach (var x in facts.Lessons)
            yield return new Candidate(XpEventType.LessonCompleted, $"lesson:{x.SourceId}", x.At, null, x.SourceId);

        foreach (var x in facts.Components)
            yield return new Candidate(XpEventType.ComponentCompleted, $"component:{x.SourceId}", x.At, null, x.SourceId);

        // Chỉ bài nộp ĐÚNG HẠN mới sinh sự kiện. Nộp trễ không bị trừ XP — phạt trễ đã có ở
        // sổ điểm (Phase 6b); trừ thêm ở đây là phạt hai lần cho cùng một lỗi.
        foreach (var x in facts.Submissions.Where(s => !s.IsLate))
            yield return new Candidate(XpEventType.AssignmentSubmittedOnTime, $"submission:{x.SourceId}", x.At, null, x.SourceId);

        foreach (var x in facts.Quizzes)
            yield return new Candidate(XpEventType.QuizHighScore, $"quiz_attempt:{x.SourceId}", x.At, x.Score, x.SourceId);
    }
}
