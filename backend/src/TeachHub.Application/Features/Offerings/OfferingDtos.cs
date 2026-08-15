using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings;

/// <param name="SubjectCode">
/// Mã và tên môn lấy kèm ngay trong truy vấn. Không có hai trường này thì danh sách lớp chỉ
/// hiện "DA24TIN01 · HK1 2026-2027" — giảng viên dạy bốn môn không đọc ra đó là lớp môn nào,
/// và giao diện buộc phải gọi thêm một vòng /api/subjects rồi tự ghép.
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
