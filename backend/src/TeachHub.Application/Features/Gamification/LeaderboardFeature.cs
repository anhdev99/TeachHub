using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

/// <param name="FullName">Chỉ dùng NỘI BỘ trong handler; không bao giờ đi thẳng ra DTO.</param>
internal sealed record LeaderboardRow(
    Guid EnrollmentId, string FullName, int TotalXp, int StreakDays, int BadgeCount);

internal static class LeaderboardBuilder
{
    /// <summary>
    /// XP lấy từ SỔ CÁI xp_events (nguồn chân lý). Streak lấy từ cột đệm
    /// <c>enrollments.streak_days</c> — tính lại streak cho 300 người ở mỗi lần mở bảng xếp
    /// hạng là 300 lượt đọc tiến độ, nên chỗ này chấp nhận con số của lần đồng bộ gần nhất.
    /// Trang tiến độ cá nhân thì luôn tính lại nên số của chính mình không bao giờ cũ.
    /// </summary>
    public static Task<List<LeaderboardRow>> ReadRowsAsync(
        IAppDbContext db, Guid offeringId, CancellationToken ct) =>
        (from e in GamificationAccess.ActiveEnrollments(db, offeringId).AsNoTracking()
         select new LeaderboardRow(
             e.Id,
             e.FullName,
             db.XpEvents.Where(x => x.EnrollmentId == e.Id).Sum(x => (int?)x.Points) ?? 0,
             e.StreakDays,
             db.EarnedBadges.Count(b => b.EnrollmentId == e.Id)))
        .ToListAsync(ct);

    /// <param name="size">
    /// Số hàng trả về. Truyền tường minh chứ KHÔNG lấy từ <paramref name="settings"/> rồi sửa:
    /// entity cấu hình không được phép bị sửa để phục vụ một lần đọc — chỉ cần một
    /// <c>SaveChanges</c> chạy sau đó trong cùng scope là giá trị tạm đó ghi đè cấu hình thật.
    /// </param>
    public static LeaderboardDto Build(
        Guid offeringId, OfferingGamificationSettings settings, int size,
        IReadOnlyList<LeaderboardRow> rows, Guid? myEnrollmentId, bool forceRealNames)
    {
        // Sắp xếp tất định: cùng điểm thì theo EnrollmentId (UUIDv7 = thứ tự ghi danh). Không có
        // tiêu chí phụ thì hai lần tải trang cho ra hai thứ hạng khác nhau ở nhóm bằng điểm, và
        // sinh viên tưởng hệ thống hỏng.
        var xepHang = rows
            .OrderByDescending(r => r.TotalXp)
            .ThenBy(r => r.EnrollmentId)
            .ToList();

        var viTri = myEnrollmentId is { } id
            ? xepHang.FindIndex(r => r.EnrollmentId == id)
            : -1;

        var hienTen = forceRealNames || settings.LeaderboardVisibility == LeaderboardVisibility.RealNames;

        var dong = xepHang
            .Take(Math.Max(size, 1))
            .Select((r, i) =>
            {
                var laToi = myEnrollmentId is { } me && r.EnrollmentId == me;

                // Ở chế độ ẩn danh KHÔNG có trường nào mang tên thật hay MSSV — kể cả trường mà
                // giao diện không hiển thị. Gửi tên xuống rồi ẩn bằng CSS là ẩn danh trên màn
                // hình và công khai trong tab Network của trình duyệt.
                var ten = hienTen ? r.FullName
                        : laToi ? "Bạn"
                        : $"Sinh viên #{i + 1}";

                return new LeaderboardRowDto(i + 1, ten, r.TotalXp, r.StreakDays, r.BadgeCount, laToi);
            })
            .ToList();

        return new LeaderboardDto(
            offeringId, settings.IsEnabled,
            hienTen ? LeaderboardVisibility.RealNames : LeaderboardVisibility.Anonymous,
            xepHang.Count,
            viTri >= 0 ? viTri + 1 : null,
            viTri >= 0 ? xepHang[viTri].TotalXp : 0,
            dong);
    }
}

// ── Bảng xếp hạng phía sinh viên ────────────────────────────────────────────────────────

public sealed record GetLeaderboardQuery(Guid OfferingId, Guid UserId) : IQuery<Result<LeaderboardDto>>;

public sealed class GetLeaderboardHandler(IAppDbContext db)
    : IQueryHandler<GetLeaderboardQuery, Result<LeaderboardDto>>
{
    public async Task<Result<LeaderboardDto>> Handle(GetLeaderboardQuery request, CancellationToken ct)
    {
        var learner = await GamificationAccess.LearnerAsync(db, request.OfferingId, request.UserId, ct);
        if (learner is null) return Result<LeaderboardDto>.NotFound("Không tìm thấy lớp học phần.");

        var settings = await GamificationEngine.ReadSettingsAsync(db, request.OfferingId, ct);

        // Tắt cơ chế thì KHÔNG đọc dữ liệu của ai cả — trả bảng rỗng thay vì đọc rồi lọc ở tầng
        // hiển thị. Đọc rồi lọc là một dòng `if` sót ở lần sửa sau nữa và cả lớp lộ điểm.
        if (!settings.IsEnabled)
            return Result<LeaderboardDto>.Ok(new LeaderboardDto(
                request.OfferingId, false, settings.LeaderboardVisibility, 0, null, 0, []));

        var rows = await LeaderboardBuilder.ReadRowsAsync(db, request.OfferingId, ct);

        return Result<LeaderboardDto>.Ok(LeaderboardBuilder.Build(
            request.OfferingId, settings, settings.LeaderboardSize,
            rows, learner.EnrollmentId, forceRealNames: false));
    }
}

// ── Bảng xếp hạng phía giảng viên ───────────────────────────────────────────────────────

/// <summary>
/// Giảng viên luôn thấy tên thật: đây là lớp của họ và họ đã thấy toàn bộ sổ điểm. Cờ ẩn danh
/// là để bảo vệ sinh viên khỏi BẠN CÙNG LỚP, không phải khỏi giảng viên — hiểu nhầm chỗ này
/// dẫn tới một màn hình mà giảng viên không dùng được vào việc gì.
/// </summary>
public sealed record GetOfferingLeaderboardQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<LeaderboardDto>>;

public sealed class GetOfferingLeaderboardHandler(IAppDbContext db)
    : IQueryHandler<GetOfferingLeaderboardQuery, Result<LeaderboardDto>>
{
    public async Task<Result<LeaderboardDto>> Handle(
        GetOfferingLeaderboardQuery request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<LeaderboardDto>.NotFound("Không tìm thấy lớp học phần.");

        var settings = await GamificationEngine.ReadSettingsAsync(db, request.OfferingId, ct);
        var rows = await LeaderboardBuilder.ReadRowsAsync(db, request.OfferingId, ct);

        // Giảng viên xem được toàn lớp, không cắt theo LeaderboardSize của màn hình sinh viên.
        return Result<LeaderboardDto>.Ok(LeaderboardBuilder.Build(
            request.OfferingId, settings, rows.Count,
            rows, myEnrollmentId: null, forceRealNames: true));
    }
}
