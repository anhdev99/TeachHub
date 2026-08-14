using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Subjects.Queries.GetSubjectVersions;

public sealed record GetSubjectVersionsQuery(Guid SubjectId, Guid InstructorId)
    : IQuery<Result<IReadOnlyList<SubjectVersionDto>>>;

public sealed class GetSubjectVersionsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetSubjectVersionsQuery, Result<IReadOnlyList<SubjectVersionDto>>>
{
    public async Task<Result<IReadOnlyList<SubjectVersionDto>>> Handle(
        GetSubjectVersionsQuery request, CancellationToken ct)
    {
        // Kiểm quyền sở hữu ở MÔN, rồi mới liệt kê phiên bản của nó. Bỏ bước này là
        // bất kỳ ai đoán được subjectId đều đọc được lịch sử phiên bản của người khác.
        var laCuaMinh = await db.Subjects.AnyAsync(
            s => s.Id == request.SubjectId && s.InstructorId == request.InstructorId, ct);

        if (!laCuaMinh)
            return Result<IReadOnlyList<SubjectVersionDto>>.NotFound("Không tìm thấy môn học.");

        var versions = await db.SubjectVersions
            .AsNoTracking()
            .Where(v => v.SubjectId == request.SubjectId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new SubjectVersionDto(
                v.Id, v.SubjectId, v.VersionLabel, v.Status,
                v.ClonedFromVersionId, v.Changelog, v.PublishedAt, v.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<SubjectVersionDto>>.Ok(versions);
    }
}
