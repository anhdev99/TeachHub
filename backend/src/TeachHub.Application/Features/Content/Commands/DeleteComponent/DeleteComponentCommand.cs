using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Commands.DeleteComponent;

public sealed record DeleteComponentCommand : ICommand<Result>
{
    public Guid ComponentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class DeleteComponentCommandHandler(IAppDbContext db)
    : ICommandHandler<DeleteComponentCommand, Result>
{
    public async Task<Result> Handle(DeleteComponentCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsComponentAsync(db, request.ComponentId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy component.");

        var lessonId = await db.LessonComponents
            .Where(c => c.Id == request.ComponentId).Select(c => c.LessonId).SingleAsync(ct);

        var lesson = await db.Lessons.Include(l => l.Components).SingleAsync(l => l.Id == lessonId, ct);

        // Đi qua entity Lesson chứ không xóa thẳng trên DbSet: Reindex() nằm trong Domain
        // và là nơi duy nhất giữ bất biến order_index liên tục.
        lesson.RemoveComponent(request.ComponentId);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
