using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Content.Queries.GetLessons;

namespace TeachHub.Application.Features.Content.Queries.GetLesson;

public sealed record GetLessonQuery(Guid LessonId, Guid InstructorId) : IQuery<Result<LessonDto>>;

public sealed class GetLessonQueryHandler(IAppDbContext db)
    : IQueryHandler<GetLessonQuery, Result<LessonDto>>
{
    public async Task<Result<LessonDto>> Handle(GetLessonQuery request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsLessonAsync(db, request.LessonId, request.InstructorId, ct))
            return Result<LessonDto>.NotFound("Không tìm thấy bài học.");

        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Components)
            .SingleAsync(l => l.Id == request.LessonId, ct);

        return Result<LessonDto>.Ok(GetLessonsQueryHandler.ToDto(lesson));
    }
}
