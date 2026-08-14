using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Offerings.Queries.GetOffering;

public sealed record GetOfferingQuery(Guid OfferingId, Guid InstructorId) : IQuery<Result<OfferingDto>>;

public sealed class GetOfferingQueryHandler(IAppDbContext db)
    : IQueryHandler<GetOfferingQuery, Result<OfferingDto>>
{
    public async Task<Result<OfferingDto>> Handle(GetOfferingQuery request, CancellationToken ct)
    {
        var o = await db.CourseOfferings
            .AsNoTracking()
            .Where(x => x.Id == request.OfferingId &&
                        db.Subjects.Any(s => s.Id == x.SubjectId && s.InstructorId == request.InstructorId))
            .Select(x => new OfferingDto(
                x.Id, x.SubjectId, x.SubjectVersionId, x.Semester, x.ClassCode,
                x.JoinCodeValue, x.JoinCodeExpiresAt, x.Status, x.TimeZoneId,
                db.Enrollments.Count(e => e.OfferingId == x.Id), x.CreatedAt))
            .SingleOrDefaultAsync(ct);

        return o is null ? Result<OfferingDto>.NotFound("Không tìm thấy lớp học phần.") : Result<OfferingDto>.Ok(o);
    }
}
