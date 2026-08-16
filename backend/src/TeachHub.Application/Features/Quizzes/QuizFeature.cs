using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Content;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Content;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Application.Features.Quizzes;

/// <summary>
/// A quiz belongs to a subject version OR to an offering, so the ownership check has to branch
/// down the right path. Gathered in one place because every Phase 5 handler needs it.
/// </summary>
public static class QuizOwnership
{
    public static async Task<bool> OwnsQuizAsync(
        IAppDbContext db, Guid quizId, Guid instructorId, CancellationToken ct)
    {
        var q = await db.Quizzes.AsNoTracking()
            .Where(x => x.Id == quizId)
            .Select(x => new { x.SubjectVersionId, x.OfferingId })
            .SingleOrDefaultAsync(ct);

        if (q is null) return false;

        return q.SubjectVersionId is { } vid
            ? await ContentOwnership.OwnsVersionAsync(db, vid, instructorId, ct)
            : q.OfferingId is { } oid
              && await OfferingOwnership.OwnsOfferingAsync(db, oid, instructorId, ct);
    }
}

// ── Create / edit a quiz ────────────────────────────────────────────────────────────────

public sealed record SaveQuizCommand(
    Guid? Id, Guid? SubjectVersionId, Guid? OfferingId,
    string Title, string? Description, QuizConfigDto Config) : ICommand<Result<QuizDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveQuizValidator : AbstractValidator<SaveQuizCommand>
{
    public SaveQuizValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Chưa nhập tên bài trắc nghiệm.").MaximumLength(300);

        // Gap #3 restated at the validator layer: the database CHECK is the last line of defence,
        // while this layer is the one that produces a readable Vietnamese sentence instead of a
        // raw constraint violation.
        RuleFor(x => x)
            .Must(x => (x.SubjectVersionId is null) != (x.OfferingId is null))
            .When(x => x.Id is null)
            .WithMessage("Bài trắc nghiệm phải thuộc ĐÚNG MỘT trong hai: phiên bản môn học, hoặc một lớp.");

        RuleFor(x => x.Config.TimeLimitMinutes).InclusiveBetween(1, 600)
            .When(x => x.Config.TimeLimitMinutes is not null)
            .WithMessage("Giới hạn thời gian phải từ 1 đến 600 phút.");

        RuleFor(x => x.Config.MaxAttempts).InclusiveBetween(1, 20)
            .When(x => x.Config.MaxAttempts is not null)
            .WithMessage("Số lần làm tối đa phải từ 1 đến 20.");
    }
}

public sealed class SaveQuizHandler(IAppDbContext db)
    : ICommandHandler<SaveQuizCommand, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> Handle(SaveQuizCommand request, CancellationToken ct)
    {
        Quiz quiz;

        if (request.Id is { } id)
        {
            if (!await QuizOwnership.OwnsQuizAsync(db, id, request.InstructorId, ct))
                return Result<QuizDto>.NotFound("Không tìm thấy bài trắc nghiệm.");

            quiz = await db.Quizzes.SingleAsync(q => q.Id == id, ct);
        }
        else
        {
            var allowed = request.SubjectVersionId is { } vid
                ? await ContentOwnership.OwnsVersionAsync(db, vid, request.InstructorId, ct)
                : request.OfferingId is { } oid
                  && await OfferingOwnership.OwnsOfferingAsync(db, oid, request.InstructorId, ct);

            if (!allowed) return Result<QuizDto>.NotFound("Không tìm thấy phiên bản môn học hoặc lớp.");

            quiz = new Quiz
            {
                SubjectVersionId = request.SubjectVersionId,
                OfferingId = request.OfferingId,
                Title = request.Title.Trim(),
            };

            db.Quizzes.Add(quiz);
        }

        quiz.Title = request.Title.Trim();
        quiz.Description = request.Description?.Trim();
        quiz.Config.IsGraded = request.Config.IsGraded;
        quiz.Config.TimeLimitMinutes = request.Config.TimeLimitMinutes;
        quiz.Config.MaxAttempts = request.Config.MaxAttempts;
        quiz.Config.ShuffleQuestions = request.Config.ShuffleQuestions;
        quiz.Config.ShuffleOptions = request.Config.ShuffleOptions;
        quiz.Config.ShowExplanationAfterSubmit = request.Config.ShowExplanationAfterSubmit;

        await db.SaveChangesAsync(ct);

        return Result<QuizDto>.Ok(QuizDto.From(quiz, await QuestionsOfQuizAsync(db, quiz.Id, ct)));
    }

    internal static async Task<List<QuizQuestion>> QuestionsOfQuizAsync(
        IAppDbContext db, Guid quizId, CancellationToken ct)
    {
        var items = await db.QuizItems.AsNoTracking()
            .Where(i => i.QuizId == quizId)
            .OrderBy(i => i.OrderIndex)
            .Select(i => new { i.QuestionId, i.PointsOverride, i.OrderIndex })
            .ToListAsync(ct);

        var ids = items.Select(m => m.QuestionId).ToList();

        var questions = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => ids.Contains(q.Id))
            .ToListAsync(ct);

        // Keep the quiz's own ordering and apply the points override where there is one.
        return [.. items
            .Select(m => (m, q: questions.FirstOrDefault(x => x.Id == m.QuestionId)))
            .Where(x => x.q is not null)
            .Select(x =>
            {
                if (x.m.PointsOverride is { } p) x.q!.Points = p;
                return x.q!;
            })];
    }
}

// ── Read a quiz (instructor side, WITH the answer key) ──────────────────────────────────

public sealed record GetQuizQuery(Guid QuizId, Guid InstructorId) : IQuery<Result<QuizDto>>;

public sealed class GetQuizHandler(IAppDbContext db) : IQueryHandler<GetQuizQuery, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> Handle(GetQuizQuery request, CancellationToken ct)
    {
        if (!await QuizOwnership.OwnsQuizAsync(db, request.QuizId, request.InstructorId, ct))
            return Result<QuizDto>.NotFound("Không tìm thấy bài trắc nghiệm.");

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == request.QuizId, ct);

        return Result<QuizDto>.Ok(
            QuizDto.From(quiz, await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct)));
    }
}

public sealed record GetQuizzesQuery(Guid? SubjectVersionId, Guid? OfferingId, Guid InstructorId)
    : IQuery<Result<List<QuizDto>>>;

public sealed class GetQuizzesHandler(IAppDbContext db)
    : IQueryHandler<GetQuizzesQuery, Result<List<QuizDto>>>
{
    public async Task<Result<List<QuizDto>>> Handle(GetQuizzesQuery request, CancellationToken ct)
    {
        var allowed = request.SubjectVersionId is { } vid
            ? await ContentOwnership.OwnsVersionAsync(db, vid, request.InstructorId, ct)
            : request.OfferingId is { } oid
              && await OfferingOwnership.OwnsOfferingAsync(db, oid, request.InstructorId, ct);

        if (!allowed) return Result<List<QuizDto>>.NotFound("Không tìm thấy phiên bản môn học hoặc lớp.");

        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(q => request.SubjectVersionId != null
                ? q.SubjectVersionId == request.SubjectVersionId
                : q.OfferingId == request.OfferingId)
            .OrderBy(q => q.Title)
            .ToListAsync(ct);

        var result = new List<QuizDto>(quizzes.Count);
        foreach (var q in quizzes)
            result.Add(QuizDto.From(q, await SaveQuizHandler.QuestionsOfQuizAsync(db, q.Id, ct)));

        return Result<List<QuizDto>>.Ok(result);
    }
}

// ── Attach questions to a quiz ──────────────────────────────────────────────────────────

public sealed record SetQuizQuestionsCommand(List<Guid> QuestionIds) : ICommand<Result<QuizDto>>
{
    public Guid QuizId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class SetQuizQuestionsHandler(IAppDbContext db)
    : ICommandHandler<SetQuizQuestionsCommand, Result<QuizDto>>
{
    public async Task<Result<QuizDto>> Handle(SetQuizQuestionsCommand request, CancellationToken ct)
    {
        if (!await QuizOwnership.OwnsQuizAsync(db, request.QuizId, request.InstructorId, ct))
            return Result<QuizDto>.NotFound("Không tìm thấy bài trắc nghiệm.");

        // Changing the question set once somebody has taken the quiz corrupts work that was
        // already graded — the old scores were computed over a different set of questions.
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == request.QuizId, ct))
            return Result<QuizDto>.Conflict(
                "Bài này đã có lượt làm. Đổi danh sách câu hỏi sẽ làm điểm đã chấm không còn giải thích được.");

        var mine = await db.QuizQuestions.AsNoTracking()
            .Where(q => q.InstructorId == request.InstructorId && request.QuestionIds.Contains(q.Id))
            .Select(q => q.Id)
            .ToListAsync(ct);

        var foreign = request.QuestionIds.Except(mine).ToList();
        if (foreign.Count > 0)
            return Result<QuizDto>.NotFound("Có câu hỏi không thuộc ngân hàng của bạn.");

        var existing = await db.QuizItems.Where(i => i.QuizId == request.QuizId).ToListAsync(ct);
        db.QuizItems.RemoveRange(existing);

        db.QuizItems.AddRange(request.QuestionIds.Distinct().Select((qid, i) => new QuizItem
        {
            QuizId = request.QuizId,
            QuestionId = qid,
            OrderIndex = i,
        }));

        await db.SaveChangesAsync(ct);

        var quiz = await db.Quizzes.AsNoTracking().SingleAsync(q => q.Id == request.QuizId, ct);

        return Result<QuizDto>.Ok(
            QuizDto.From(quiz, await SaveQuizHandler.QuestionsOfQuizAsync(db, quiz.Id, ct)));
    }
}
