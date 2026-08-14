using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Offerings.Queries.GetOfferings;

public sealed record GetOfferingsQuery(Guid InstructorId, Guid? SubjectId = null)
    : IQuery<IReadOnlyList<OfferingDto>>;

public sealed class GetOfferingsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetOfferingsQuery, IReadOnlyList<OfferingDto>>
{
    public async Task<IReadOnlyList<OfferingDto>> Handle(GetOfferingsQuery request, CancellationToken ct) =>
        // Điều kiện sở hữu nằm TRONG câu truy vấn (ADR-012), không nạp rồi lọc.
        await db.CourseOfferings
            .AsNoTracking()
            .Where(x => db.Subjects.Any(s => s.Id == x.SubjectId && s.InstructorId == request.InstructorId))
            .Where(x => request.SubjectId == null || x.SubjectId == request.SubjectId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OfferingDto(
                x.Id, x.SubjectId, x.SubjectVersionId, x.Semester, x.ClassCode,
                x.JoinCodeValue, x.JoinCodeExpiresAt, x.Status, x.TimeZoneId,
                db.Enrollments.Count(e => e.OfferingId == x.Id), x.CreatedAt))
            .ToListAsync(ct);
}
