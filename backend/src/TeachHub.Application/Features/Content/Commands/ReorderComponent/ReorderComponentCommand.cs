using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Commands.ReorderComponent;

public sealed record ReorderComponentCommand(int ToIndex) : ICommand<Result>
{
    public Guid ComponentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ReorderComponentCommandValidator : AbstractValidator<ReorderComponentCommand>
{
    public ReorderComponentCommandValidator() =>
        RuleFor(x => x.ToIndex).GreaterThanOrEqualTo(0).WithMessage("Vị trí đích không được âm.");
}

public sealed class ReorderComponentCommandHandler(IAppDbContext db)
    : ICommandHandler<ReorderComponentCommand, Result>
{
    public async Task<Result> Handle(ReorderComponentCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsComponentAsync(db, request.ComponentId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy component.");

        var lessonId = await db.LessonComponents
            .Where(c => c.Id == request.ComponentId).Select(c => c.LessonId).SingleAsync(ct);

        var lesson = await db.Lessons.Include(l => l.Components).SingleAsync(l => l.Id == lessonId, ct);

        lesson.MoveComponent(request.ComponentId, request.ToIndex);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
