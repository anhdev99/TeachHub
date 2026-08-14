using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Enrollments.Queries.GetEnrollments;

public sealed record EnrollmentDto(
    Guid Id, Guid OfferingId, string StudentCode, string FullName,
    string? Email, string? ClassName, EnrollmentStatus Status, bool HasAccount);

public sealed record GetEnrollmentsQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<IReadOnlyList<EnrollmentDto>>>;

public sealed class GetEnrollmentsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetEnrollmentsQuery, Result<IReadOnlyList<EnrollmentDto>>>
{
    public async Task<Result<IReadOnlyList<EnrollmentDto>>> Handle(
        GetEnrollmentsQuery request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<IReadOnlyList<EnrollmentDto>>.NotFound("Không tìm thấy lớp học phần.");

        var ds = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.OfferingId == request.OfferingId)
            .OrderBy(e => e.StudentCode)
            .Select(e => new EnrollmentDto(
                e.Id, e.OfferingId, e.StudentCode, e.FullName, e.Email, e.ClassName,
                e.Status, e.UserId != null))
            .ToListAsync(ct);

        return Result<IReadOnlyList<EnrollmentDto>>.Ok(ds);
    }
}
