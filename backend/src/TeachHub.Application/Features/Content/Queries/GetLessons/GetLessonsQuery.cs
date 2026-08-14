using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Queries.GetLessons;

public sealed record GetLessonsQuery(Guid ChapterId, Guid InstructorId)
    : IQuery<Result<IReadOnlyList<LessonDto>>>;

public sealed class GetLessonsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetLessonsQuery, Result<IReadOnlyList<LessonDto>>>
{
    public async Task<Result<IReadOnlyList<LessonDto>>> Handle(GetLessonsQuery request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsChapterAsync(db, request.ChapterId, request.InstructorId, ct))
            return Result<IReadOnlyList<LessonDto>>.NotFound("Không tìm thấy chương.");

        var lessons = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Components)
            .Where(l => l.ChapterId == request.ChapterId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(ct);

        return Result<IReadOnlyList<LessonDto>>.Ok([.. lessons.Select(ToDto)]);
    }

    internal static LessonDto ToDto(Domain.Content.Lesson l) => new(
        l.Id, l.ChapterId, l.Title, l.OrderIndex, l.ContentType, l.FileUrl, l.TextContent,
        l.DurationMinutes, l.IsRequiredToUnlockNext,
        [.. l.Components.OrderBy(c => c.OrderIndex)
             .Select(c => new LessonComponentDto(
                 c.Id, c.LessonId, c.Type, c.OrderIndex, c.RefId, c.IsRequired, c.Config))]);
}
