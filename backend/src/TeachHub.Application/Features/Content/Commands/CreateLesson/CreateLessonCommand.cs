using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Content.Commands.CreateLesson;

public sealed record CreateLessonCommand(
    string Title,
    LessonContentType ContentType = LessonContentType.Text,
    string? TextContent = null,
    int DurationMinutes = 0,
    bool IsRequiredToUnlockNext = false) : ICommand<Result<LessonDto>>
{
    public Guid ChapterId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CreateLessonCommandValidator : AbstractValidator<CreateLessonCommand>
{
    public CreateLessonCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Chưa nhập tiêu đề bài học.")
            .MaximumLength(300).WithMessage("Tiêu đề bài học dài quá 300 ký tự.");
        RuleFor(x => x.DurationMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Thời lượng không được âm.");
    }
}

public sealed class CreateLessonCommandHandler(IAppDbContext db)
    : ICommandHandler<CreateLessonCommand, Result<LessonDto>>
{
    public async Task<Result<LessonDto>> Handle(CreateLessonCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsChapterAsync(db, request.ChapterId, request.InstructorId, ct))
            return Result<LessonDto>.NotFound("Không tìm thấy chương.");

        var soBai = await db.Lessons.CountAsync(l => l.ChapterId == request.ChapterId, ct);

        var lesson = new Lesson
        {
            ChapterId = request.ChapterId,
            Title = request.Title,
            ContentType = request.ContentType,
            TextContent = request.TextContent,
            DurationMinutes = request.DurationMinutes,
            IsRequiredToUnlockNext = request.IsRequiredToUnlockNext,
            OrderIndex = soBai,
        };

        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(ct);

        return Result<LessonDto>.Ok(new LessonDto(
            lesson.Id, lesson.ChapterId, lesson.Title, lesson.OrderIndex, lesson.ContentType,
            lesson.FileUrl, lesson.TextContent, lesson.DurationMinutes,
            lesson.IsRequiredToUnlockNext, []));
    }
}
