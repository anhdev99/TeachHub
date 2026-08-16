using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings;

/// <param name="SubjectCode">
/// The subject code and name are fetched by the same query. Without these two fields the offering
/// list only shows "DA24TIN01 · HK1 2026-2027" — an instructor teaching four subjects cannot tell
/// which subject that offering belongs to, and the UI is forced into an extra round trip to
/// /api/subjects and has to stitch the data together itself.
/// </param>
public sealed record OfferingDto(
    Guid Id,
    Guid SubjectId,
    Guid SubjectVersionId,
    string SubjectCode,
    string SubjectName,
    string Semester,
    string ClassCode,
    string? JoinCode,
    DateTimeOffset? JoinCodeExpiresAt,
    OfferingStatus Status,
    string TimeZoneId,
    int EnrollmentCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// A course offering belongs to an instructor INDIRECTLY, through Subject.InstructorId. The check
/// is gathered into one place because every Phase 2 handler needs it — each hand-copied version is
/// one more place where the condition can be forgotten, and forgetting it in a single spot leaks
/// another instructor's student roster.
/// </summary>
public static class OfferingOwnership
{
    public static Task<bool> OwnsOfferingAsync(
        IAppDbContext db, Guid offeringId, Guid instructorId, CancellationToken ct) =>
        db.CourseOfferings.AnyAsync(o => o.Id == offeringId &&
            db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == instructorId), ct);
}
