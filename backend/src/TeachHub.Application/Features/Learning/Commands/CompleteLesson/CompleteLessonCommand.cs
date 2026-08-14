using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Commands.CompleteLesson;

public sealed record CompleteLessonCommand(Guid LessonId, Guid UserId, int SecondsSpent = 0)
    : ICommand<Result>;

public sealed class CompleteLessonCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CompleteLessonCommand, Result>
{
    public async Task<Result> Handle(CompleteLessonCommand request, CancellationToken ct)
    {
        var enrollmentId = await LearningAccess.EnrollmentForLessonAsync(
            db, request.LessonId, request.UserId, ct);

        if (enrollmentId is null) return Result.NotFound("Không tìm thấy bài học.");

        var now = clock.GetUtcNow();

        // Cập nhật trước, chèn sau. Sinh viên bấm nhanh hai lần là hai transaction song song;
        // unique index (enrollment_id, lesson_id) sẽ chặn bản thứ hai và ta nuốt đúng lỗi đó
        // thay vì trả 500. "Kiểm rồi chèn" không đủ: transaction thứ hai không nhìn thấy
        // dòng mà transaction thứ nhất chưa commit.
        var hienCo = await db.LessonProgresses.SingleOrDefaultAsync(
            p => p.EnrollmentId == enrollmentId && p.LessonId == request.LessonId, ct);

        if (hienCo is not null)
        {
            hienCo.Status = ProgressStatus.Completed;
            hienCo.CompletedAt ??= now;
            hienCo.SecondsSpent += request.SecondsSpent;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }

        db.LessonProgresses.Add(new LessonProgress
        {
            EnrollmentId = enrollmentId.Value,
            LessonId = request.LessonId,
            Status = ProgressStatus.Completed,
            CompletedAt = now,
            SecondsSpent = request.SecondsSpent,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Không await được trong bộ lọc `when` của catch (CS7094), nên kiểm trong thân.
            // Request song song đã chèn trước ⇒ kết quả người dùng mong đợi vẫn đạt được.
            if (!await DaTonTaiAsync(enrollmentId.Value, request.LessonId, ct)) throw;
        }

        return Result.Ok();
    }

    private Task<bool> DaTonTaiAsync(Guid enrollmentId, Guid lessonId, CancellationToken ct) =>
        db.LessonProgresses.AnyAsync(p => p.EnrollmentId == enrollmentId && p.LessonId == lessonId, ct);
}
