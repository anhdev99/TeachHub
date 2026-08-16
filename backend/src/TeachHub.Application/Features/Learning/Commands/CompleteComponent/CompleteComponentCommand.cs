using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Commands.CompleteComponent;

public sealed record CompleteComponentCommand(Guid ComponentId, Guid UserId) : ICommand<Result>
{
    /// <summary>
    /// This command came from the GRADING PATH (<c>CheckInlineQuestionsCommand</c>) and not from
    /// the student's "Đánh dấu đã xong" button.
    ///
    /// Only that path may mark a <c>checkpoint_quiz</c> component as done: when a student clicks
    /// done on a quiz without having answered a single question, "the required component is
    /// complete" proves nothing at all, and it unlocks the next lesson exactly as for someone who
    /// answered the whole thing correctly.
    ///
    /// Safe to keep here because the controller builds the command entirely server-side — the id
    /// comes from the route, the user from the claim, NOTHING is bound from the request body. If
    /// this command is ever model-bound from a body, this flag has to come out, otherwise a student
    /// sending <c>true</c> themselves walks straight past the check.
    /// </summary>
    public bool FromGradingPath { get; init; }
}

public sealed class CompleteComponentCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CompleteComponentCommand, Result>
{
    public async Task<Result> Handle(CompleteComponentCommand request, CancellationToken ct)
    {
        var enrollmentId = await LearningAccess.EnrollmentForComponentAsync(
            db, request.ComponentId, request.UserId, ct);

        if (enrollmentId is null) return Result.NotFound("Không tìm thấy phần học.");

        if (await OfferingLock.IsClosedForEnrollmentAsync(db, enrollmentId.Value, ct))
            return Result.Conflict(OfferingLock.ClosedMessage("đánh dấu hoàn thành"));

        // Trắc nghiệm nhúng CÓ CÂU HỎI thì chỉ đường chấm mới đánh dấu xong được.
        //
        // Trước đây sinh viên bấm "Đánh dấu đã xong" là qua, không cần trả lời câu nào — nên
        // một phần bắt buộc "đã hoàn thành" không chứng minh được điều gì, và nó mở khoá bài
        // sau y hệt người làm đúng cả bài. Có đường chấm thật rồi thì cửa sau này phải đóng.
        //
        // Điều kiện `InlineQuestions.Count > 0` KHÔNG thừa: dữ liệu cũ có phần checkpoint_quiz
        // không tải được câu hỏi (soạn trước khi có `inline_questions`). Chặn cả những phần đó
        // là nhốt sinh viên lại — không có câu nào để trả lời thì không có cách nào đi tiếp.
        if (!request.FromGradingPath)
        {
            var coCauHoi = await db.LessonComponents.AsNoTracking()
                .AnyAsync(c => c.Id == request.ComponentId
                               && c.Type == LessonComponentType.CheckpointQuiz
                               && c.Config.InlineQuestions.Count > 0, ct);

            if (coCauHoi)
                return Result.Conflict(
                    "Phần trắc nghiệm này phải trả lời rồi mới đánh dấu hoàn thành được.");
        }

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
