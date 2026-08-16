using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Application.Features.Submissions;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Integrity;

/// <param name="Content">
/// NULL while the hint is still locked. This is a security boundary, not a bandwidth
/// optimisation: ship the content along and let the UI hide it, and opening DevTools reads
/// every hint without losing a single point, turning the whole Gap #11 deduction mechanism
/// into decoration.
/// </param>
public sealed record HintDto(
    Guid Id, int OrderIndex, decimal PenaltyPercent,
    bool IsUnlocked, string? Content, DateTimeOffset? UnlockedAt);

// ── Student lists the hints ─────────────────────────────────────────────────────────────

public sealed record GetMyHintsQuery(Guid AssignmentId, Guid StudentUserId)
    : IQuery<Result<List<HintDto>>>;

public sealed class GetMyHintsHandler(IAppDbContext db)
    : IQueryHandler<GetMyHintsQuery, Result<List<HintDto>>>
{
    public async Task<Result<List<HintDto>>> Handle(GetMyHintsQuery request, CancellationToken ct)
    {
        var enrollment = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        // Not enrolled in the offering that runs this assignment ⇒ 404, never 403.
        if (enrollment is null) return Result<List<HintDto>>.NotFound("Không tìm thấy bài tập.");

        var hints = await db.AssignmentHints.AsNoTracking()
            .Where(h => h.AssignmentId == request.AssignmentId)
            .OrderBy(h => h.OrderIndex)
            .ToListAsync(ct);

        var unlocked = await db.HintUnlocks.AsNoTracking()
            .Where(u => u.EnrollmentId == enrollment.EnrollmentId
                     && hints.Select(h => h.Id).Contains(u.AssignmentHintId))
            .ToDictionaryAsync(u => u.AssignmentHintId, ct);

        return Result<List<HintDto>>.Ok([.. hints.Select(h =>
        {
            var unlock = unlocked.GetValueOrDefault(h.Id);

            return new HintDto(
                h.Id, h.OrderIndex, h.PenaltyPercent,
                unlock is not null, unlock is null ? null : h.Content, unlock?.UnlockedAt);
        })]);
    }
}

// ── Student unlocks one hint ────────────────────────────────────────────────────────────

public sealed record UnlockHintCommand(Guid HintId, Guid StudentUserId) : ICommand<Result<HintDto>>;

public sealed class UnlockHintHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<UnlockHintCommand, Result<HintDto>>
{
    public async Task<Result<HintDto>> Handle(UnlockHintCommand request, CancellationToken ct)
    {
        var hint = await db.AssignmentHints.AsNoTracking()
            .SingleOrDefaultAsync(h => h.Id == request.HintId, ct);

        if (hint is null) return Result<HintDto>.NotFound("Không tìm thấy gợi ý.");

        var enrollment = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, hint.AssignmentId, request.StudentUserId, ct);

        if (enrollment is null) return Result<HintDto>.NotFound("Không tìm thấy gợi ý.");

        // Offering already finished ⇒ no further hints may be unlocked. Hints ALREADY unlocked
        // stay readable as usual through GET /api/assignments/{id}/hints — the student has paid
        // for that content in points, and locking the read path too takes back what they bought.
        if (await OfferingLock.IsClosedAsync(db, enrollment.OfferingId, ct))
            return Result<HintDto>.Conflict(OfferingLock.ClosedMessage("mở gợi ý"));

        var alreadyUnlocked = await db.HintUnlocks
            .SingleOrDefaultAsync(u => u.AssignmentHintId == hint.Id
                                    && u.EnrollmentId == enrollment.EnrollmentId, ct);

        // Unlocking an already-unlocked hint is perfectly normal (double click, page reload) and
        // must NOT deduct points a second time. Return Ok with the content rather than Conflict:
        // the student did nothing wrong, and an error message here only makes them think they
        // have lost points all over again.
        if (alreadyUnlocked is not null)
            return Result<HintDto>.Ok(new HintDto(
                hint.Id, hint.OrderIndex, alreadyUnlocked.PenaltyPercentApplied,
                true, hint.Content, alreadyUnlocked.UnlockedAt));

        // "PROGRESSIVE hints" (SRS UC-09): every earlier hint must be unlocked first. Allow a
        // jump straight to the last hint — usually the one that gives away the most — and the
        // hint ladder stops being a ladder.
        var earlierIds = await db.AssignmentHints.AsNoTracking()
            .Where(h => h.AssignmentId == hint.AssignmentId && h.OrderIndex < hint.OrderIndex)
            .Select(h => h.Id)
            .ToListAsync(ct);

        if (earlierIds.Count > 0)
        {
            var unlockedCount = await db.HintUnlocks
                .CountAsync(u => u.EnrollmentId == enrollment.EnrollmentId
                              && earlierIds.Contains(u.AssignmentHintId), ct);

            if (unlockedCount < earlierIds.Count)
                return Result<HintDto>.Invalid("Hãy mở lần lượt các gợi ý trước đó.");
        }

        var now = clock.GetUtcNow();

        db.HintUnlocks.Add(new HintUnlock
        {
            AssignmentHintId = hint.Id,
            EnrollmentId = enrollment.EnrollmentId,
            UnlockedAt = now,

            // Freeze the penalty right here. Read it back off AssignmentHint at grading time and
            // an instructor who edits the penalty afterwards retroactively changes the terms the
            // student accepted on screen.
            PenaltyPercentApplied = hint.PenaltyPercent,
        });

        // HintsUsed on the current submission is nothing but a number to display on the grading
        // screen. The source of truth for the deduction is always the hint_unlocks table — it
        // holds who/when/how much, whereas a bare counter explains nothing when challenged.
        var current = await db.Submissions
            .SingleOrDefaultAsync(s => s.AssignmentId == hint.AssignmentId
                                    && s.EnrollmentId == enrollment.EnrollmentId
                                    && s.IsCurrent, ct);

        if (current is not null) current.HintsUsed += 1;

        await db.SaveChangesAsync(ct);

        return Result<HintDto>.Ok(new HintDto(
            hint.Id, hint.OrderIndex, hint.PenaltyPercent, true, hint.Content, now));
    }
}

// ── Instructor sees who unlocked which hint ─────────────────────────────────────────────

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

        // Filter on BOTH the assignment AND the offering inside the query itself: the same
        // assignment runs in several offerings, and filtering after loading means pulling
        // another offering's log across the network only to throw it away.
        var rows = await (from u in db.HintUnlocks.AsNoTracking()
                          join h in db.AssignmentHints.AsNoTracking() on u.AssignmentHintId equals h.Id
                          join e in db.Enrollments.AsNoTracking() on u.EnrollmentId equals e.Id
                          where h.AssignmentId == request.AssignmentId && e.OfferingId == request.OfferingId
                          orderby u.UnlockedAt descending
                          select new HintUnlockLogDto(
                              h.Id, h.OrderIndex, e.Id, e.FullName,
                              u.PenaltyPercentApplied, u.UnlockedAt))
                         .ToListAsync(ct);

        return Result<List<HintUnlockLogDto>>.Ok(rows);
    }
}
