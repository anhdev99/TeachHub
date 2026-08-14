using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Commands.ReorderLesson;

public sealed record ReorderLessonCommand(int ToIndex) : ICommand<Result>
{
    public Guid LessonId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ReorderLessonCommandValidator : AbstractValidator<ReorderLessonCommand>
{
    public ReorderLessonCommandValidator() =>
        RuleFor(x => x.ToIndex).GreaterThanOrEqualTo(0).WithMessage("Vị trí đích không được âm.");
}

public sealed class ReorderLessonCommandHandler(IAppDbContext db)
    : ICommandHandler<ReorderLessonCommand, Result>
{
    public async Task<Result> Handle(ReorderLessonCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsLessonAsync(db, request.LessonId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy bài học.");

        var lesson = await db.Lessons.SingleAsync(l => l.Id == request.LessonId, ct);

        var anhEm = await db.Lessons
            .Where(l => l.ChapterId == lesson.ChapterId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(ct);

        anhEm.Remove(anhEm.Single(l => l.Id == lesson.Id));
        anhEm.Insert(Math.Clamp(request.ToIndex, 0, anhEm.Count), lesson);

        // Đánh lại toàn bộ 0..n-1 thay vì chỉ hoán đổi hai phần tử: chỉ có cách này mới
        // bảo đảm order_index liên tục sau nhiều lần kéo-thả.
        for (var i = 0; i < anhEm.Count; i++) anhEm[i].OrderIndex = i;

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
