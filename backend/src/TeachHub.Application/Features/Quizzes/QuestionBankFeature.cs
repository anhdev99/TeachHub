using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Application.Features.Quizzes;

// ── Đọc ngân hàng câu hỏi ───────────────────────────────────────────────────────────────

public sealed record GetQuestionsQuery(Guid InstructorId, string? Tag) : IQuery<List<QuizQuestionDto>>;

public sealed class GetQuestionsHandler(IAppDbContext db)
    : IQueryHandler<GetQuestionsQuery, List<QuizQuestionDto>>
{
    public async Task<List<QuizQuestionDto>> Handle(GetQuestionsQuery request, CancellationToken ct)
    {
        var ds = await db.QuizQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.InstructorId == request.InstructorId)
            .Where(q => request.Tag == null || q.FoundationTags.Contains(request.Tag))
            .OrderByDescending(q => q.Id)
            .ToListAsync(ct);

        return [.. ds.Select(QuizQuestionDto.From)];
    }
}

// ── Tạo / sửa câu hỏi ───────────────────────────────────────────────────────────────────

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

        // Luật thuần hình dạng trên chính payload ⇒ validator. Một câu trắc nghiệm không có
        // đáp án đúng nào là câu không ai trả lời đúng được — lỗi soạn đề, không phải lựa chọn.
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
        QuizQuestion cau;
        var moi = request.Id is null;

        if (request.Id is { } id)
        {
            var tim = await db.QuizQuestions.Include(q => q.Options)
                .SingleOrDefaultAsync(q => q.Id == id && q.InstructorId == request.InstructorId, ct);

            if (tim is null) return Result<QuizQuestionDto>.NotFound("Không tìm thấy câu hỏi.");

            cau = tim;

            // Remove/Add tường minh qua DbSet: Entity.Id tự sinh UUIDv7 lúc khởi tạo nên thêm
            // con qua navigation của parent đang tracked làm EF phát UPDATE thay vì INSERT.
            db.QuestionOptions.RemoveRange(cau.Options);
            cau.Options.Clear();
        }
        else
        {
            cau = new QuizQuestion
            {
                InstructorId = request.InstructorId,
                Type = request.Type,
                Text = request.Text.Trim(),
            };

            db.QuizQuestions.Add(cau);
        }

        cau.Type = request.Type;
        cau.Text = request.Text.Trim();
        cau.Explanation = request.Explanation?.Trim();
        cau.FoundationTags = [.. request.FoundationTags.Select(t => t.Trim()).Where(t => t.Length > 0)];
        cau.Points = request.Points;
        cau.AllowPartialCredit = request.AllowPartialCredit;

        cau.FillBlank = request.Type == QuestionType.FillBlank && request.FillBlank is { } f
            ? new FillBlankConfig
            {
                AcceptedAnswers = [.. f.AcceptedAnswers.Select(a => a.Trim()).Where(a => a.Length > 0)],
                CaseSensitive = f.CaseSensitive,
                TrimWhitespace = f.TrimWhitespace,
                NormalizeUnicode = f.NormalizeUnicode,
            }
            : null;

        var pa = request.Options.Select((o, i) => new QuestionOption
        {
            QuestionId = cau.Id,
            Text = o.Text.Trim(),
            IsCorrect = o.IsCorrect,
            OrderIndex = i,
        }).ToList();

        if (moi) cau.Options.AddRange(pa);          // parent cũng mới ⇒ cả đồ thị Added
        else db.QuestionOptions.AddRange(pa);

        await db.SaveChangesAsync(ct);

        // Nạp lại để DTO có đúng danh sách phương án vừa ghi.
        var luu = await db.QuizQuestions.AsNoTracking().Include(q => q.Options)
            .SingleAsync(q => q.Id == cau.Id, ct);

        return Result<QuizQuestionDto>.Ok(QuizQuestionDto.From(luu));
    }
}

// ── Xóa câu hỏi ─────────────────────────────────────────────────────────────────────────

public sealed record DeleteQuestionCommand(Guid Id, Guid InstructorId) : ICommand<Result>;

public sealed class DeleteQuestionHandler(IAppDbContext db)
    : ICommandHandler<DeleteQuestionCommand, Result>
{
    public async Task<Result> Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var cau = await db.QuizQuestions
            .SingleOrDefaultAsync(q => q.Id == request.Id && q.InstructorId == request.InstructorId, ct);

        if (cau is null) return Result.NotFound("Không tìm thấy câu hỏi.");

        // Câu đang nằm trong quiz đã có người làm thì xóa là làm hỏng bài đã chấm: lượt làm
        // lưu id câu trong answers_json, và điểm cũ sẽ không giải thích được nữa.
        var soLuot = await db.QuizAttempts.CountAsync(
            a => db.QuizItems.Any(i => i.QuizId == a.QuizId && i.QuestionId == request.Id), ct);

        if (soLuot > 0)
            return Result.Conflict(
                $"Câu này đã có {soLuot} lượt làm bài. Xóa đi thì điểm đã chấm không còn giải thích được.");

        db.QuizQuestions.Remove(cau);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
