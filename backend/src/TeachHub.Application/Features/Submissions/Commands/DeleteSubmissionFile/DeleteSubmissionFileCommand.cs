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

        var baiNop = await db.Submissions
            .SingleOrDefaultAsync(s => s.Id == file.SubmissionId, ct);

        // Không phải bài của mình ⇒ 404, không bao giờ 403.
        if (baiNop is null ||
            !await db.Enrollments.AnyAsync(
                e => e.Id == baiNop.EnrollmentId && e.UserId == request.StudentUserId, ct))
            return Result.NotFound("Không tìm thấy tệp.");

        if (await OfferingLock.DaDongAsync(db, baiNop.OfferingId, ct))
            return Result.Conflict(OfferingLock.ThongDiep("xóa tệp"));

        // Đã nộp chính thức thì tệp là bằng chứng chấm điểm, không xóa được nữa. Muốn thay
        // thì nộp lại — lượt mới giữ nguyên lượt cũ trong lịch sử (Gap #5).
        if (baiNop.Status != SubmissionStatus.Draft)
            return Result.Conflict(
                "Bài đã nộp chính thức nên không xóa tệp được. Tải tệp mới lên để bắt đầu một lần nộp lại.");

        db.SubmissionFiles.Remove(file);
        await db.SaveChangesAsync(ct);

        // Xóa tệp vật lý SAU khi DB đã commit. Làm ngược lại thì storage lỗi giữa chừng để
        // lại một hàng trỏ vào tệp không còn tồn tại — và trang nộp bài sẽ hỏng mãi mãi.
        if (file.FilePath is not null)
        {
            try
            {
                await storage.DeleteAsync(file.FilePath, ct);
            }
            catch (IOException)
            {
                // Hàng đã xóa rồi; tệp mồ côi trên đĩa không làm hỏng gì và dọn được sau.
            }
        }

        return Result.Ok();
    }
}
