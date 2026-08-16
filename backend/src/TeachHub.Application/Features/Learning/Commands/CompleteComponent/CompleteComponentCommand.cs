using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Commands.CompleteComponent;

public sealed record CompleteComponentCommand(Guid ComponentId, Guid UserId) : ICommand<Result>
{
    /// <summary>
    /// Lệnh này đến từ ĐƯỜNG CHẤM (<c>CheckInlineQuestionsCommand</c>) chứ không phải từ nút
    /// "Đánh dấu đã xong" của sinh viên.
    ///
    /// Chỉ có nó mới được phép đánh dấu xong một phần <c>checkpoint_quiz</c>: sinh viên tự bấm
    /// xong một bài trắc nghiệm mà chưa trả lời câu nào thì "phần bắt buộc đã hoàn thành" không
    /// chứng minh được gì, và nó mở khoá bài kế tiếp y như người làm đúng cả bài.
    ///
    /// An toàn khi để ở đây vì controller dựng lệnh hoàn toàn phía máy chủ — id lấy từ route,
    /// người dùng lấy từ claim, KHÔNG bind thân request. Nếu ngày nào lệnh này được model-bind
    /// từ body thì phải bỏ cờ này ra, nếu không sinh viên tự gửi <c>true</c> là qua mặt được.
    /// </summary>
    public bool QuaDuongCham { get; init; }
}

public sealed class CompleteComponentCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CompleteComponentCommand, Result>
{
    public async Task<Result> Handle(CompleteComponentCommand request, CancellationToken ct)
    {
        var enrollmentId = await LearningAccess.EnrollmentForComponentAsync(
            db, request.ComponentId, request.UserId, ct);

        if (enrollmentId is null) return Result.NotFound("Không tìm thấy phần học.");

        if (await OfferingLock.DaDongTheoGhiDanhAsync(db, enrollmentId.Value, ct))
            return Result.Conflict(OfferingLock.ThongDiep("đánh dấu hoàn thành"));

        // Trắc nghiệm nhúng CÓ CÂU HỎI thì chỉ đường chấm mới đánh dấu xong được.
        //
        // Trước đây sinh viên bấm "Đánh dấu đã xong" là qua, không cần trả lời câu nào — nên
        // một phần bắt buộc "đã hoàn thành" không chứng minh được điều gì, và nó mở khoá bài
        // sau y hệt người làm đúng cả bài. Có đường chấm thật rồi thì cửa sau này phải đóng.
        //
        // Điều kiện `InlineQuestions.Count > 0` KHÔNG thừa: dữ liệu cũ có phần checkpoint_quiz
        // không tải được câu hỏi (soạn trước khi có `inline_questions`). Chặn cả những phần đó
        // là nhốt sinh viên lại — không có câu nào để trả lời thì không có cách nào đi tiếp.
        if (!request.QuaDuongCham)
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
