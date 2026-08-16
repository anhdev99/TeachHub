using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;

namespace TeachHub.Application.Features.Review;

/// <summary>
/// A student answers one item in the review queue. Graded by the very same <see cref="QuizGrader"/>
/// from Phase 5 — re-implementing the grading rules here is exactly how the two copies drift apart,
/// and the copy inside the review feature would be the one with no Vietnamese NFC tests behind it.
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

        // The ownership condition lives INSIDE the query, and somebody else's item returns 404, not
        // 403: a 403 confirms that the id really exists, which means walking ids in sequence would
        // let you count another person's queue.
        var item = await db.ReviewQueueItems
            .SingleOrDefaultAsync(i => i.Id == request.ItemId && i.UserId == request.UserId, ct);

        if (item is null) return Result<ReviewAnswerDto>.NotFound("Không tìm thấy mục ôn tập.");

        if (item.Status != ReviewItemStatus.Pending)
            return Result<ReviewAnswerDto>.Conflict("Bạn đã thuộc mục này, không cần ôn lại nữa.");

        // Not due yet means we do NOT accept the answer. It sounds harsh, but this IS the mechanism:
        // without the block, a student who answers correctly three times within half a minute pushes
        // the item to "mastered", and the entire value of SPACED repetition — the gap between two
        // acts of recall — is wiped out.
        // Applied to wrong answers too so there is only ONE rule: the UI only shows due items anyway.
        if (!item.IsDue(now))
            return Result<ReviewAnswerDto>.Conflict("Mục này chưa tới hạn ôn tập.");

        var question = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .SingleOrDefaultAsync(q => q.Id == item.QuestionId, ct);

        if (question is null)
            return Result<ReviewAnswerDto>.NotFound("Câu hỏi này đã bị gỡ khỏi ngân hàng.");

        var graded = QuizGrader.Grade(
            [question], [new QuizAnswer(question.Id, request.SelectedOptionIds ?? [], request.Text)]);

        var isCorrect = graded.PerQuestion[0].IsCorrect;

        item.ApplyAnswer(isCorrect, now);
        await db.SaveChangesAsync(ct);

        return Result<ReviewAnswerDto>.Ok(new ReviewAnswerDto(
            item.Id, isCorrect, item.Stage, item.NextReviewAt,
            item.Status == ReviewItemStatus.Mastered,
            question.Explanation,
            [.. question.Options.Where(o => o.IsCorrect).Select(o => o.Id)],
            question.FillBlank?.AcceptedAnswers ?? []));
    }
}
