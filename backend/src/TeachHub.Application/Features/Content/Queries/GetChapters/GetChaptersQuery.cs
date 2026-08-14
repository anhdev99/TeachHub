using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Queries.GetChapters;

public sealed record GetChaptersQuery(Guid SubjectVersionId, Guid InstructorId)
    : IQuery<Result<IReadOnlyList<ChapterDto>>>;

public sealed class GetChaptersQueryHandler(IAppDbContext db)
    : IQueryHandler<GetChaptersQuery, Result<IReadOnlyList<ChapterDto>>>
{
    public async Task<Result<IReadOnlyList<ChapterDto>>> Handle(GetChaptersQuery request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsVersionAsync(db, request.SubjectVersionId, request.InstructorId, ct))
            return Result<IReadOnlyList<ChapterDto>>.NotFound("Không tìm thấy phiên bản.");

        var chapters = await db.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectVersionId == request.SubjectVersionId)
            .OrderBy(c => c.OrderIndex)
            .Select(c => new ChapterDto(
                c.Id, c.SubjectVersionId, c.Title, c.Objective, c.OrderIndex, c.FoundationTags, c.UnlockRule))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ChapterDto>>.Ok(chapters);
    }
}
