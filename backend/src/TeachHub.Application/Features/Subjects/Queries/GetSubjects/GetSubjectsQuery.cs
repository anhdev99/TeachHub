using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Subjects.Queries.GetSubjects;

public sealed record GetSubjectsQuery(Guid InstructorId) : IQuery<IReadOnlyList<SubjectDto>>;

public sealed class GetSubjectsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    public async Task<IReadOnlyList<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken ct) =>
        // Lọc instructor_id NGAY trong câu truy vấn (ADR-012), không nạp rồi lọc trong bộ nhớ.
        await db.Subjects
            .AsNoTracking()
            .Where(s => s.InstructorId == request.InstructorId)
            .OrderBy(s => s.Code)
            .Select(s => new SubjectDto(s.Id, s.Code, s.Name, s.Description, s.CurrentVersionId, s.CreatedAt))
            .ToListAsync(ct);
}
