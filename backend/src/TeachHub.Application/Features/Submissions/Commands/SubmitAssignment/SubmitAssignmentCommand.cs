using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Commands.SubmitAssignment;

public sealed record SubmitAssignmentCommand(Guid AssignmentId, Guid StudentUserId)
    : ICommand<Result<SubmissionDto>>;

public sealed class SubmitAssignmentHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SubmitAssignmentCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(SubmitAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await db.Assignments
            .Include(a => a.Slots)
            .SingleOrDefaultAsync(a => a.Id == request.AssignmentId, ct);

        if (assignment is null) return Result<SubmissionDto>.NotFound("Không tìm thấy bài tập.");

        var enrollment = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        if (enrollment is null) return Result<SubmissionDto>.NotFound("Không tìm thấy bài tập.");

        // The offering has ended ⇒ stop BEFORE every other check. Push this further down and a
        // student submitting after the offering closed gets back "Chưa nộp các phần bắt buộc" —
        // a message that is both wrong and sends them off to upload more files to an offering
        // that no longer accepts any work.
        if (await OfferingLock.IsClosedAsync(db, enrollment.OfferingId, ct))
            return Result<SubmissionDto>.Conflict(OfferingLock.ClosedMessage("nộp bài"));

        var draft = await db.Submissions
            .Include(s => s.Files)
            .SingleOrDefaultAsync(s => s.AssignmentId == request.AssignmentId
                                    && s.EnrollmentId == enrollment.EnrollmentId
                                    && s.IsCurrent
                                    && s.Status == SubmissionStatus.Draft, ct);

        if (draft is null) return Result<SubmissionDto>.Invalid("Chưa có tệp nào được tải lên.");

        var now = clock.GetUtcNow();

        if (assignment.DueAt is not null && now > assignment.DueAt && !assignment.AllowLateSubmission)
            return Result<SubmissionDto>.Forbidden(
                $"Bài tập đã hết hạn lúc {assignment.DueAt:dd/MM/yyyy HH:mm} và không nhận nộp trễ.");

        var present = draft.Files
            .GroupBy(f => f.SlotKey)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        if (!assignment.CanBeSubmittedWith(present))
        {
            // Name the missing parts explicitly. Just saying "not eligible yet" means the student
            // clicks the button five more times and then messages to ask why.
            var missing = assignment.MissingRequiredSlots(present).Select(s => s.Label);
            return Result<SubmissionDto>.Invalid($"Chưa nộp các phần bắt buộc: {string.Join(", ", missing)}.");
        }

        // Resubmission: every earlier version moves into the history, it is NEVER deleted
        // (Gap #5). Do not call BeginTransactionAsync here — TransactionBehavior already wraps
        // every ICommand (ADR-010), so clearing the is_current flag and stamping the new version
        // either both land or neither does.
        var previous = await db.Submissions
            .Where(s => s.AssignmentId == request.AssignmentId
                     && s.EnrollmentId == enrollment.EnrollmentId
                     && s.IsCurrent && s.Id != draft.Id)
            .ToListAsync(ct);

        foreach (var p in previous) p.IsCurrent = false;

        draft.MarkSubmitted(now, assignment.DueAt);

        await db.SaveChangesAsync(ct);

        return Result<SubmissionDto>.Ok(SubmissionDto.From(draft));
    }
}
