using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Submissions;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Integrity;

/// <param name="Content">
/// NULL khi gợi ý chưa mở. Đây là ranh giới bảo mật, không phải tối ưu băng thông: gửi kèm
/// nội dung rồi để giao diện che đi thì mở DevTools là đọc được toàn bộ gợi ý mà không mất
/// điểm nào, và cả cơ chế trừ điểm của Gap #11 trở thành trang trí.
/// </param>
public sealed record HintDto(
    Guid Id, int OrderIndex, decimal PenaltyPercent,
    bool IsUnlocked, string? Content, DateTimeOffset? UnlockedAt);

// ── Sinh viên xem danh sách gợi ý ───────────────────────────────────────────────────────

public sealed record GetMyHintsQuery(Guid AssignmentId, Guid StudentUserId)
    : IQuery<Result<List<HintDto>>>;

public sealed class GetMyHintsHandler(IAppDbContext db)
    : IQueryHandler<GetMyHintsQuery, Result<List<HintDto>>>
{
    public async Task<Result<List<HintDto>>> Handle(GetMyHintsQuery request, CancellationToken ct)
    {
        var ghiDanh = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        // Không học lớp chạy bài tập này ⇒ 404, không bao giờ 403.
        if (ghiDanh is null) return Result<List<HintDto>>.NotFound("Không tìm thấy bài tập.");

        var goiY = await db.AssignmentHints.AsNoTracking()
            .Where(h => h.AssignmentId == request.AssignmentId)
            .OrderBy(h => h.OrderIndex)
            .ToListAsync(ct);

        var daMo = await db.HintUnlocks.AsNoTracking()
            .Where(u => u.EnrollmentId == ghiDanh.EnrollmentId
                     && goiY.Select(h => h.Id).Contains(u.AssignmentHintId))
            .ToDictionaryAsync(u => u.AssignmentHintId, ct);

        return Result<List<HintDto>>.Ok([.. goiY.Select(h =>
        {
            var mo = daMo.GetValueOrDefault(h.Id);

            return new HintDto(
                h.Id, h.OrderIndex, h.PenaltyPercent,
                mo is not null, mo is null ? null : h.Content, mo?.UnlockedAt);
        })]);
    }
}

// ── Sinh viên mở một gợi ý ──────────────────────────────────────────────────────────────

public sealed record UnlockHintCommand(Guid HintId, Guid StudentUserId) : ICommand<Result<HintDto>>;

public sealed class UnlockHintHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<UnlockHintCommand, Result<HintDto>>
{
    public async Task<Result<HintDto>> Handle(UnlockHintCommand request, CancellationToken ct)
    {
        var goiY = await db.AssignmentHints.AsNoTracking()
            .SingleOrDefaultAsync(h => h.Id == request.HintId, ct);

        if (goiY is null) return Result<HintDto>.NotFound("Không tìm thấy gợi ý.");

        var ghiDanh = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, goiY.AssignmentId, request.StudentUserId, ct);

        if (ghiDanh is null) return Result<HintDto>.NotFound("Không tìm thấy gợi ý.");

        var daMo = await db.HintUnlocks
            .SingleOrDefaultAsync(u => u.AssignmentHintId == goiY.Id
                                    && u.EnrollmentId == ghiDanh.EnrollmentId, ct);

        // Mở lại gợi ý đã mở là chuyện bình thường (bấm hai lần, tải lại trang) và KHÔNG được
        // trừ điểm lần nữa. Trả Ok với nội dung thay vì Conflict: sinh viên không làm gì sai,
        // và một thông báo lỗi ở đây chỉ khiến họ tưởng mất điểm thêm lần nữa.
        if (daMo is not null)
            return Result<HintDto>.Ok(new HintDto(
                goiY.Id, goiY.OrderIndex, daMo.PenaltyPercentApplied,
                true, goiY.Content, daMo.UnlockedAt));

        // "Gợi ý MỞ DẦN" (SRS UC-09): phải mở hết các gợi ý trước đó. Cho nhảy thẳng tới gợi
        // ý cuối — thường là gợi ý lộ nhiều nhất — thì thang gợi ý không còn là một thang.
        var idTruoc = await db.AssignmentHints.AsNoTracking()
            .Where(h => h.AssignmentId == goiY.AssignmentId && h.OrderIndex < goiY.OrderIndex)
            .Select(h => h.Id)
            .ToListAsync(ct);

        if (idTruoc.Count > 0)
        {
            var soDaMo = await db.HintUnlocks
                .CountAsync(u => u.EnrollmentId == ghiDanh.EnrollmentId
                              && idTruoc.Contains(u.AssignmentHintId), ct);

            if (soDaMo < idTruoc.Count)
                return Result<HintDto>.Invalid("Hãy mở lần lượt các gợi ý trước đó.");
        }

        var now = clock.GetUtcNow();

        db.HintUnlocks.Add(new HintUnlock
        {
            AssignmentHintId = goiY.Id,
            EnrollmentId = ghiDanh.EnrollmentId,
            UnlockedAt = now,

            // Chốt mức phạt tại đây. Đọc lại từ AssignmentHint lúc chấm thì giảng viên sửa
            // mức phạt sau đó sẽ đổi ngược điều kiện mà sinh viên đã chấp nhận trên màn hình.
            PenaltyPercentApplied = goiY.PenaltyPercent,
        });

        // HintsUsed trên bài nộp hiện hành chỉ là con số hiển thị cho màn hình chấm. Nguồn
        // chân lý để trừ điểm luôn là bảng hint_unlocks — nó có đủ ai/lúc nào/trừ bao nhiêu,
        // còn một con số đếm thì không giải thích được gì khi bị chất vấn.
        var hienHanh = await db.Submissions
            .SingleOrDefaultAsync(s => s.AssignmentId == goiY.AssignmentId
                                    && s.EnrollmentId == ghiDanh.EnrollmentId
                                    && s.IsCurrent, ct);

        if (hienHanh is not null) hienHanh.HintsUsed += 1;

        await db.SaveChangesAsync(ct);

        return Result<HintDto>.Ok(new HintDto(
            goiY.Id, goiY.OrderIndex, goiY.PenaltyPercent, true, goiY.Content, now));
    }
}

// ── Giảng viên xem ai đã mở gợi ý nào ───────────────────────────────────────────────────

public sealed record HintUnlockLogDto(
    Guid HintId, int OrderIndex, Guid EnrollmentId, string StudentName,
    decimal PenaltyPercentApplied, DateTimeOffset UnlockedAt);

public sealed record GetHintUnlockLogQuery(Guid AssignmentId, Guid OfferingId, Guid InstructorId)
    : IQuery<Result<List<HintUnlockLogDto>>>;

public sealed class GetHintUnlockLogHandler(IAppDbContext db)
    : IQueryHandler<GetHintUnlockLogQuery, Result<List<HintUnlockLogDto>>>
{
    public async Task<Result<List<HintUnlockLogDto>>> Handle(
        GetHintUnlockLogQuery request, CancellationToken ct)
    {
        if (!await Offerings.OfferingOwnership.OwnsOfferingAsync(
                db, request.OfferingId, request.InstructorId, ct))
            return Result<List<HintUnlockLogDto>>.NotFound("Không tìm thấy lớp học phần.");

        // Lọc theo CẢ bài tập LẪN lớp ngay trong câu truy vấn: cùng một bài tập chạy ở nhiều
        // lớp, và lọc sau khi nạp là kéo nhật ký của lớp khác qua mạng trước khi bỏ đi.
        var ds = await (from u in db.HintUnlocks.AsNoTracking()
                        join h in db.AssignmentHints.AsNoTracking() on u.AssignmentHintId equals h.Id
                        join e in db.Enrollments.AsNoTracking() on u.EnrollmentId equals e.Id
                        where h.AssignmentId == request.AssignmentId && e.OfferingId == request.OfferingId
                        orderby u.UnlockedAt descending
                        select new HintUnlockLogDto(
                            h.Id, h.OrderIndex, e.Id, e.FullName,
                            u.PenaltyPercentApplied, u.UnlockedAt))
                       .ToListAsync(ct);

        return Result<List<HintUnlockLogDto>>.Ok(ds);
    }
}
