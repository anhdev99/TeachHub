using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// Sinh viên trả lời một mục trong hàng đợi ôn tập. Chấm bằng đúng <see cref="QuizGrader"/> của
/// Phase 5 — chép lại luật chấm ở đây là để hai bản lệch nhau, và bản trong tính năng ôn tập sẽ
/// là bản không có test tiếng Việt NFC.
/// </summary>
public sealed record AnswerReviewItemCommand(List<Guid> SelectedOptionIds, string? Text)
    : ICommand<Result<ReviewAnswerDto>>
{
    public Guid ItemId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class AnswerReviewItemHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<AnswerReviewItemCommand, Result<ReviewAnswerDto>>
{
    public async Task<Result<ReviewAnswerDto>> Handle(
        AnswerReviewItemCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Điều kiện sở hữu nằm TRONG câu truy vấn, và mục của người khác trả 404 chứ không 403:
        // 403 xác nhận rằng id đó có thật, tức là dò id tuần tự sẽ đếm được hàng đợi của người khác.
        var muc = await db.ReviewQueueItems
            .SingleOrDefaultAsync(i => i.Id == request.ItemId && i.UserId == request.UserId, ct);

        if (muc is null) return Result<ReviewAnswerDto>.NotFound("Không tìm thấy mục ôn tập.");

        if (muc.Status != ReviewItemStatus.Pending)
            return Result<ReviewAnswerDto>.Conflict("Bạn đã thuộc mục này, không cần ôn lại nữa.");

        // Chưa tới hạn thì KHÔNG nhận. Nghe khắt khe nhưng đây chính là cơ chế: không chặn thì
        // sinh viên bấm đúng ba lần trong nửa phút là mục lên "đã thuộc", và toàn bộ giá trị
        // của ôn tập NGẮT QUÃNG — khoảng cách giữa hai lần nhớ lại — bị xóa sạch.
        // Áp cho cả câu trả lời sai để chỉ có MỘT luật: giao diện vốn chỉ hiện mục đến hạn.
        if (!muc.IsDue(now))
            return Result<ReviewAnswerDto>.Conflict("Mục này chưa tới hạn ôn tập.");

        var cau = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .SingleOrDefaultAsync(q => q.Id == muc.QuestionId, ct);

        if (cau is null)
            return Result<ReviewAnswerDto>.NotFound("Câu hỏi này đã bị gỡ khỏi ngân hàng.");

        var kq = QuizGrader.Grade(
            [cau], [new QuizAnswer(cau.Id, request.SelectedOptionIds ?? [], request.Text)]);

        var dung = kq.PerQuestion[0].IsCorrect;

        muc.ApplyAnswer(dung, now);
        await db.SaveChangesAsync(ct);

        return Result<ReviewAnswerDto>.Ok(new ReviewAnswerDto(
            muc.Id, dung, muc.Stage, muc.NextReviewAt,
            muc.Status == ReviewItemStatus.Mastered,
            cau.Explanation,
            [.. cau.Options.Where(o => o.IsCorrect).Select(o => o.Id)],
            cau.FillBlank?.AcceptedAnswers ?? []));
    }
}
