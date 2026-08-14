using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Commands.CompleteComponent;

public sealed record CompleteComponentCommand(Guid ComponentId, Guid UserId) : ICommand<Result>;

public sealed class CompleteComponentCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CompleteComponentCommand, Result>
{
    public async Task<Result> Handle(CompleteComponentCommand request, CancellationToken ct)
    {
        var enrollmentId = await LearningAccess.EnrollmentForComponentAsync(
            db, request.ComponentId, request.UserId, ct);

        if (enrollmentId is null) return Result.NotFound("Không tìm thấy phần học.");

        var now = clock.GetUtcNow();

        var hienCo = await db.ComponentProgresses.SingleOrDefaultAsync(
            p => p.EnrollmentId == enrollmentId && p.LessonComponentId == request.ComponentId, ct);

        if (hienCo is not null)
        {
            hienCo.Status = ProgressStatus.Completed;
            hienCo.CompletedAt ??= now;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }

        db.ComponentProgresses.Add(new ComponentProgress
        {
            EnrollmentId = enrollmentId.Value,
            LessonComponentId = request.ComponentId,
            Status = ProgressStatus.Completed,
            CompletedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Không await được trong bộ lọc `when` của catch (CS7094), nên kiểm trong thân.
            // Request song song đã chèn trước ⇒ kết quả người dùng mong đợi vẫn đạt được.
            if (!await DaTonTaiAsync(enrollmentId.Value, request.ComponentId, ct)) throw;
        }

        return Result.Ok();
    }

    private Task<bool> DaTonTaiAsync(Guid enrollmentId, Guid componentId, CancellationToken ct) =>
        db.ComponentProgresses.AnyAsync(
            p => p.EnrollmentId == enrollmentId && p.LessonComponentId == componentId, ct);
}
