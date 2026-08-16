using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Application.Features.Quizzes;

// ── Read the question bank ──────────────────────────────────────────────────────────────

public sealed record GetQuestionsQuery(Guid InstructorId, string? Tag) : IQuery<List<QuizQuestionDto>>;

public sealed class GetQuestionsHandler(IAppDbContext db)
    : IQueryHandler<GetQuestionsQuery, List<QuizQuestionDto>>
{
    public async Task<List<QuizQuestionDto>> Handle(GetQuestionsQuery request, CancellationToken ct)
    {
        var questions = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.InstructorId == request.InstructorId)
            .Where(q => request.Tag == null || q.FoundationTags.Contains(request.Tag))
            .OrderByDescending(q => q.Id)
            .ToListAsync(ct);

        return [.. questions.Select(QuizQuestionDto.From)];
    }
}

// ── Create / edit a question ────────────────────────────────────────────────────────────

public sealed record NewOption(string Text, bool IsCorrect);

public sealed record SaveQuestionCommand(
    Guid? Id, QuestionType Type, string Text, string? Explanation,
    List<string> FoundationTags, decimal Points, bool AllowPartialCredit,
    List<NewOption> Options, FillBlankDto? FillBlank) : ICommand<Result<QuizQuestionDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class SaveQuestionValidator : AbstractValidator<SaveQuestionCommand>
{
    public SaveQuestionValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Chưa nhập nội dung câu hỏi.").MaximumLength(2000);
        RuleFor(x => x.Points).GreaterThan(0m).WithMessage("Điểm của câu phải lớn hơn 0.");

        // A pure shape rule over the payload itself ⇒ validator. A multiple-choice question with
        // no correct option at all is a question nobody can answer correctly — an authoring
        // mistake, not a deliberate choice.
        RuleFor(x => x.Options)
            .Must(o => o.Count >= 2)
            .When(x => x.Type is QuestionType.SingleChoice or QuestionType.MultipleChoice)
            .WithMessage("Câu trắc nghiệm phải có ít nhất 2 phương án.");

        RuleFor(x => x.Options)
            .Must(o => o.Count(y => y.IsCorrect) == 1)
            .When(x => x.Type is QuestionType.SingleChoice or QuestionType.TrueFalse)
            .WithMessage("Câu chọn một phải có ĐÚNG một phương án đúng.");

        RuleFor(x => x.Options)
            .Must(o => o.Any(y => y.IsCorrect))
            .When(x => x.Type == QuestionType.MultipleChoice)
            .WithMessage("Câu chọn nhiều phải có ít nhất một phương án đúng.");

        RuleFor(x => x.FillBlank)
            .NotNull().Must(f => f!.AcceptedAnswers.Count > 0)
            .When(x => x.Type == QuestionType.FillBlank)
            .WithMessage("Câu điền khuyết phải khai ít nhất một đáp án được chấp nhận.");
    }
}

public sealed class SaveQuestionHandler(IAppDbContext db)
    : ICommandHandler<SaveQuestionCommand, Result<QuizQuestionDto>>
{
    public async Task<Result<QuizQuestionDto>> Handle(SaveQuestionCommand request, CancellationToken ct)
    {
        QuizQuestion question;
        var isNew = request.Id is null;

        if (request.Id is { } id)
        {
            var found = await db.QuizQuestions.Include(q => q.Options)
                .SingleOrDefaultAsync(q => q.Id == id && q.InstructorId == request.InstructorId, ct);

            if (found is null) return Result<QuizQuestionDto>.NotFound("Không tìm thấy câu hỏi.");

            question = found;

            // Explicit Remove/Add through the DbSet: Entity.Id generates its own UUIDv7 at
            // construction time, so adding children through the navigation of a tracked parent
            // makes EF emit an UPDATE instead of an INSERT.
            db.QuestionOptions.RemoveRange(question.Options);
            question.Options.Clear();
        }
        else
        {
            question = new QuizQuestion
            {
                InstructorId = request.InstructorId,
                Type = request.Type,
                Text = request.Text.Trim(),
            };

            db.QuizQuestions.Add(question);
        }

        question.Type = request.Type;
        question.Text = request.Text.Trim();
        question.Explanation = request.Explanation?.Trim();
        question.FoundationTags = [.. request.FoundationTags.Select(t => t.Trim()).Where(t => t.Length > 0)];
        question.Points = request.Points;
        question.AllowPartialCredit = request.AllowPartialCredit;

        question.FillBlank = request.Type == QuestionType.FillBlank && request.FillBlank is { } f
            ? new FillBlankConfig
            {
                AcceptedAnswers = [.. f.AcceptedAnswers.Select(a => a.Trim()).Where(a => a.Length > 0)],
                CaseSensitive = f.CaseSensitive,
                TrimWhitespace = f.TrimWhitespace,
                NormalizeUnicode = f.NormalizeUnicode,
            }
            : null;

        var options = request.Options.Select((o, i) => new QuestionOption
        {
            QuestionId = question.Id,
            Text = o.Text.Trim(),
            IsCorrect = o.IsCorrect,
            OrderIndex = i,
        }).ToList();

        if (isNew) question.Options.AddRange(options);   // parent is new too ⇒ whole graph Added
        else db.QuestionOptions.AddRange(options);

        await db.SaveChangesAsync(ct);

        // Reload so the DTO carries exactly the option list that was just written.
        var saved = await db.QuizQuestions.AsNoTracking().Include(q => q.Options)
            .SingleAsync(q => q.Id == question.Id, ct);

        return Result<QuizQuestionDto>.Ok(QuizQuestionDto.From(saved));
    }
}

// ── Delete a question ───────────────────────────────────────────────────────────────────

public sealed record DeleteQuestionCommand(Guid Id, Guid InstructorId) : ICommand<Result>;

public sealed class DeleteQuestionHandler(IAppDbContext db)
    : ICommandHandler<DeleteQuestionCommand, Result>
{
    public async Task<Result> Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await db.QuizQuestions
            .SingleOrDefaultAsync(q => q.Id == request.Id && q.InstructorId == request.InstructorId, ct);

        if (question is null) return Result.NotFound("Không tìm thấy câu hỏi.");

        // Deleting a question that sits in a quiz somebody has already taken corrupts work that
        // was already graded: an attempt stores question ids in answers_json, and the old score
        // would no longer be explainable.
        var attemptCount = await db.QuizAttempts.CountAsync(
            a => db.QuizItems.Any(i => i.QuizId == a.QuizId && i.QuestionId == request.Id), ct);

        if (attemptCount > 0)
            return Result.Conflict(
                $"Câu này đã có {attemptCount} lượt làm bài. Xóa đi thì điểm đã chấm không còn giải thích được.");

        db.QuizQuestions.Remove(question);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
