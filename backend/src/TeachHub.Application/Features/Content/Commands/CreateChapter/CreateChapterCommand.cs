using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Content.Commands.CreateChapter;

public sealed record CreateChapterCommand(
    string Title,
    string? Objective = null,
    List<string>? FoundationTags = null,
    UnlockRule UnlockRule = UnlockRule.Free) : ICommand<Result<ChapterDto>>
{
    public Guid SubjectVersionId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CreateChapterCommandValidator : AbstractValidator<CreateChapterCommand>
{
    public CreateChapterCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Chưa nhập tiêu đề chương.")
            .MaximumLength(300).WithMessage("Tiêu đề chương dài quá 300 ký tự.");
        RuleFor(x => x.Objective)
            .MaximumLength(2000).WithMessage("Mục tiêu chương dài quá 2000 ký tự.");
    }
}

public sealed class CreateChapterCommandHandler(IAppDbContext db)
    : ICommandHandler<CreateChapterCommand, Result<ChapterDto>>
{
    public async Task<Result<ChapterDto>> Handle(CreateChapterCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsVersionAsync(db, request.SubjectVersionId, request.InstructorId, ct))
            return Result<ChapterDto>.NotFound("Không tìm thấy phiên bản.");

        // OrderIndex nối vào cuối: đếm ngay trong DB thay vì nạp cả danh sách.
        var soChuong = await db.Chapters.CountAsync(c => c.SubjectVersionId == request.SubjectVersionId, ct);

        var chapter = new Chapter
        {
            SubjectVersionId = request.SubjectVersionId,
            Title = request.Title,
            Objective = request.Objective,
            FoundationTags = request.FoundationTags ?? [],
            UnlockRule = request.UnlockRule,
            OrderIndex = soChuong,
        };

        db.Chapters.Add(chapter);
        await db.SaveChangesAsync(ct);

        return Result<ChapterDto>.Ok(new ChapterDto(
            chapter.Id, chapter.SubjectVersionId, chapter.Title, chapter.Objective,
            chapter.OrderIndex, chapter.FoundationTags, chapter.UnlockRule));
    }
}
