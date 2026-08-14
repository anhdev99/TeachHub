using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings;

public sealed record OfferingDto(
    Guid Id,
    Guid SubjectId,
    Guid SubjectVersionId,
    string Semester,
    string ClassCode,
    string? JoinCode,
    DateTimeOffset? JoinCodeExpiresAt,
    OfferingStatus Status,
    string TimeZoneId,
    int EnrollmentCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// Lớp học phần thuộc về giảng viên GIÁN TIẾP qua Subject.InstructorId. Gom phép kiểm về một
/// chỗ vì mọi handler của Phase 2 đều cần nó — mỗi bản sao chép thủ công là một chỗ có thể
/// quên điều kiện, mà quên một chỗ là lộ danh sách sinh viên của lớp người khác.
/// </summary>
public static class OfferingOwnership
{
    public static Task<bool> OwnsOfferingAsync(
        IAppDbContext db, Guid offeringId, Guid instructorId, CancellationToken ct) =>
        db.CourseOfferings.AnyAsync(o => o.Id == offeringId &&
            db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == instructorId), ct);
}
