using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Commands.DeleteSubmissionFile;

public sealed record DeleteSubmissionFileCommand(Guid FileId, Guid StudentUserId) : ICommand<Result>;

public sealed class DeleteSubmissionFileHandler(IAppDbContext db, IFileStorage storage)
    : ICommandHandler<DeleteSubmissionFileCommand, Result>
{
    public async Task<Result> Handle(DeleteSubmissionFileCommand request, CancellationToken ct)
    {
        var file = await db.SubmissionFiles
            .SingleOrDefaultAsync(f => f.Id == request.FileId, ct);

        if (file is null) return Result.NotFound("Không tìm thấy tệp.");

        var submission = await db.Submissions
            .SingleOrDefaultAsync(s => s.Id == file.SubmissionId, ct);

        // Not their own submission ⇒ 404, never 403.
        if (submission is null ||
            !await db.Enrollments.AnyAsync(
                e => e.Id == submission.EnrollmentId && e.UserId == request.StudentUserId, ct))
            return Result.NotFound("Không tìm thấy tệp.");

        if (await OfferingLock.IsClosedAsync(db, submission.OfferingId, ct))
            return Result.Conflict(OfferingLock.ClosedMessage("xóa tệp"));

        // Once the work has been formally submitted, the files are the evidence the grade rests
        // on, so they can no longer be deleted. To replace one, submit again — the new attempt
        // keeps the old attempt intact in the history (Gap #5).
        if (submission.Status != SubmissionStatus.Draft)
            return Result.Conflict(
                "Bài đã nộp chính thức nên không xóa tệp được. Tải tệp mới lên để bắt đầu một lần nộp lại.");

        db.SubmissionFiles.Remove(file);
        await db.SaveChangesAsync(ct);

        // Delete the physical file AFTER the DB has committed. The other way round, a storage
        // failure halfway through leaves behind a row pointing at a file that no longer exists —
        // and the submission page stays broken forever.
        if (file.FilePath is not null)
        {
            try
            {
                await storage.DeleteAsync(file.FilePath, ct);
            }
            catch (IOException)
            {
                // The row is already gone; an orphan file on disk breaks nothing and can be
                // swept up later.
            }
        }

        return Result.Ok();
    }
}
