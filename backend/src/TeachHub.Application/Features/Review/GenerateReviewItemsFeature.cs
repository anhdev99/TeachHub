using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// Nạp hàng đợi ôn tập từ một lượt làm quiz đã nộp.
///
/// Command chứ không phải query, nên <c>TransactionBehavior</c> bọc sẵn: các mục ôn tập và dấu
/// "đã nạp" phải vào database CÙNG NHAU. Ghi mục mà mất dấu là lần chạy sau nạp lại lượt đó và
/// đẩy bậc của sinh viên về 3 ngày; ghi dấu mà mất mục là mất luôn các câu vừa sai.
///
/// Idempotent theo <see cref="ReviewGenerationMark"/>: gọi lại với cùng một lượt làm không sinh
/// thêm gì. Đây là điều kiện sống còn để job quét hằng ngày chạy được — xem chú thích của
/// <see cref="ReviewGenerationMark"/>.
/// </summary>
public sealed record GenerateReviewItemsCommand(Guid QuizAttemptId)
    : ICommand<Result<GenerateReviewResultDto>>;

public sealed class GenerateReviewItemsHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<GenerateReviewItemsCommand, Result<GenerateReviewResultDto>>
{
    public async Task<Result<GenerateReviewResultDto>> Handle(
        GenerateReviewItemsCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var luot = await db.QuizAttempts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.QuizAttemptId, ct);

        if (luot is null)
            return Result<GenerateReviewResultDto>.NotFound("Không tìm thấy lượt làm bài.");

        if (luot.Status == QuizAttemptStatus.InProgress)
            return Result<GenerateReviewResultDto>.Conflict("Lượt làm này chưa nộp, chưa có gì để ôn.");

        // Đã nạp rồi thì DỪNG NGAY, trước cả khi đọc thêm gì. Unique index trên
        // review_generation_marks.quiz_attempt_id là lớp chặn cuối nếu hai lần chạy job chồng nhau.
        if (await db.ReviewGenerationMarks.AnyAsync(m => m.QuizAttemptId == luot.Id, ct))
            return Result<GenerateReviewResultDto>.Ok(new GenerateReviewResultDto(0, 0, true));

        var quiz = await db.Quizzes.AsNoTracking()
            .SingleOrDefaultAsync(q => q.Id == luot.QuizId, ct);

        if (quiz is null)
            return Result<GenerateReviewResultDto>.NotFound("Không tìm thấy bài trắc nghiệm.");

        // CHỈ quiz tính điểm mới nạp hàng đợi. Checkpoint quiz hiện đáp án ngay sau khi trả lời,
        // nên sai ở đó là một bước trong lúc học chứ không phải lỗ hổng kiến thức còn tồn lại —
        // nạp cả checkpoint vào hàng đợi là làm ngập hàng đợi bằng thứ sinh viên vừa được sửa.
        // KHÔNG ghi dấu ở nhánh này: nếu về sau giảng viên bật tính điểm thì lượt cũ vẫn còn
        // nguyên trạng, và quyết định có nạp hay không vẫn nằm ở luật chứ không ở dấu đã ghi.
        if (!quiz.Config.IsGraded)
            return Result<GenerateReviewResultDto>.Ok(new GenerateReviewResultDto(0, 0, false));

        // Gap #2: hàng đợi gắn với CON NGƯỜI. Lượt làm gắn với enrollment, nên phải bắc cầu
        // sang user_id ở đây. Enrollment chưa gắn tài khoản (danh sách import trước, sinh viên
        // kích hoạt sau) thì chưa có ai để gắn hàng đợi vào — bỏ qua và KHÔNG ghi dấu, để lần
        // chạy sau nạp lại khi tài khoản đã được ghép.
        var nguoiHoc = await db.Enrollments.AsNoTracking()
            .Where(e => e.Id == luot.EnrollmentId)
            .Select(e => e.UserId)
            .SingleOrDefaultAsync(ct);

        if (nguoiHoc is not { } userId)
            return Result<GenerateReviewResultDto>.Ok(new GenerateReviewResultDto(0, 0, false));

        var cauSai = luot.WrongQuestionIds.Distinct().ToList();

        // Chỉ nhận câu CÒN trong ngân hàng: giảng viên xóa một câu sau khi sinh viên đã làm là
        // chuyện thường, và INSERT một mục trỏ tới câu đã xóa sẽ chết vì khóa ngoại — làm hỏng
        // cả lần nạp thay vì bỏ qua đúng một câu.
        var conTonTai = cauSai.Count == 0
            ? []
            : await db.QuizQuestions.AsNoTracking()
                .Where(q => cauSai.Contains(q.Id))
                .Select(q => q.Id)
                .ToListAsync(ct);

        // KHÔNG AsNoTracking: các mục này bị sửa ngay bên dưới.
        var daCo = conTonTai.Count == 0
            ? []
            : await db.ReviewQueueItems
                .Where(i => i.UserId == userId && conTonTai.Contains(i.QuestionId))
                .ToListAsync(ct);

        var taoMoi = 0;
        var datLai = 0;

        foreach (var questionId in conTonTai)
        {
            var muc = daCo.FirstOrDefault(i => i.QuestionId == questionId);

            if (muc is null)
            {
                // Add tường minh vào DbSet. Thêm vào collection của một entity đang được theo
                // dõi thì EF phát UPDATE thay vì INSERT (Id đã khác default) và mục mới im lặng
                // không bao giờ xuất hiện.
                db.ReviewQueueItems.Add(ReviewQueueItem.Create(userId, questionId, luot.Id, now));
                taoMoi++;
            }
            else
            {
                // Sai lại một câu đã có trong hàng đợi — kể cả câu đã "thuộc" — là quay về đầu
                // chu kỳ. SourceQuizAttemptId giữ nguyên lượt làm ĐẦU TIÊN sinh ra mục.
                muc.ApplyAnswer(dung: false, now);
                datLai++;
            }
        }

        db.ReviewGenerationMarks.Add(new ReviewGenerationMark
        {
            QuizAttemptId = luot.Id,
            GeneratedAt = now,
            ItemsCreated = taoMoi,
            ItemsReset = datLai,
        });

        await db.SaveChangesAsync(ct);

        return Result<GenerateReviewResultDto>.Ok(
            new GenerateReviewResultDto(taoMoi, datLai, false));
    }
}
