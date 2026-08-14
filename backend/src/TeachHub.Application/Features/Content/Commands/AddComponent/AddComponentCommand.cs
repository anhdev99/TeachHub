using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Content.Commands.AddComponent;

public sealed record AddComponentCommand(
    LessonComponentType Type,
    Guid? RefId = null,
    ComponentConfig? Config = null,
    bool IsRequired = false) : ICommand<Result<LessonComponentDto>>
{
    public Guid LessonId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class AddComponentCommandValidator : AbstractValidator<AddComponentCommand>
{
    public AddComponentCommandValidator()
    {
        // Luật vẫn nằm ở Domain (LessonComponent.RequiresRef + Lesson.AddComponent vẫn ném lỗi);
        // validator chỉ mượn lại nó để đổi 500 thành 400 có thông điệp đọc được.
        RuleFor(x => x.RefId).NotNull()
            .When(x => LessonComponent.RequiresRef(x.Type))
            .WithMessage(x => $"Component loại {x.Type} bắt buộc phải trỏ tới một bản ghi.");

        RuleFor(x => x.RefId).Null()
            .When(x => !LessonComponent.RequiresRef(x.Type))
            .WithMessage(x => $"Component loại {x.Type} mang nội dung nhúng, không được có ref_id.");
    }
}

public sealed class AddComponentCommandHandler(IAppDbContext db)
    : ICommandHandler<AddComponentCommand, Result<LessonComponentDto>>
{
    public async Task<Result<LessonComponentDto>> Handle(AddComponentCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsLessonAsync(db, request.LessonId, request.InstructorId, ct))
            return Result<LessonComponentDto>.NotFound("Không tìm thấy bài học.");

        var lesson = await db.Lessons
            .Include(l => l.Components)
            .SingleAsync(l => l.Id == request.LessonId, ct);

        // RefId phải trỏ tới bản ghi thuộc CHÍNH phiên bản này. Không kiểm thì một component
        // trỏ sang assignment của môn khác, và clone sẽ dừng lại vì dữ liệu không nhất quán.
        if (request.RefId is { } refId)
        {
            var versionId = await db.Chapters
                .Where(c => c.Id == lesson.ChapterId)
                .Select(c => c.SubjectVersionId)
                .SingleAsync(ct);

            var hopLe = request.Type == LessonComponentType.Assignment
                ? await db.Assignments.AnyAsync(a => a.Id == refId && a.SubjectVersionId == versionId, ct)
                : await db.Quizzes.AnyAsync(q => q.Id == refId && q.SubjectVersionId == versionId, ct);

            if (!hopLe)
                return Result<LessonComponentDto>.Invalid(
                    "Bản ghi được tham chiếu không thuộc phiên bản này.");
        }

        var component = lesson.AddComponent(request.Type, request.RefId, request.Config ?? new ComponentConfig());
        component.IsRequired = request.IsRequired;

        // Add tường minh: Id đã được sinh sẵn ở tầng ứng dụng (Entity dùng Guid.CreateVersion7),
        // nên khi thêm vào navigation của một Lesson ĐANG được theo dõi, EF suy ra trạng thái
        // Modified và phát UPDATE trên một hàng chưa tồn tại → DbUpdateConcurrencyException
        // "expected to affect 1 row(s), but actually affected 0". Gọi Add là ép đúng Added.
        db.LessonComponents.Add(component);

        await db.SaveChangesAsync(ct);

        return Result<LessonComponentDto>.Ok(new LessonComponentDto(
            component.Id, component.LessonId, component.Type, component.OrderIndex,
            component.RefId, component.IsRequired, component.Config));
    }
}
