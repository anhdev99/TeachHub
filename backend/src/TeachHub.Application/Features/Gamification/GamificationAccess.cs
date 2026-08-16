using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Gamification;

/// <param name="TimeZoneId">Múi giờ của lớp — mọi phép tính "ngày" của streak đi qua nó.</param>
public sealed record OfferingContext(Guid OfferingId, Guid InstructorId, string TimeZoneId);

public sealed record LearnerContext(
    Guid EnrollmentId, Guid OfferingId, Guid InstructorId, string TimeZoneId);

/// <summary>
/// Gom mọi phép kiểm quyền của nhóm tính năng động lực học tập về MỘT chỗ. Mỗi bản sao chép
/// thủ công là một chỗ có thể quên <c>instructor_id</c>, và quên một chỗ ở đây là lộ bảng xếp
/// hạng cùng tiến độ học của lớp người khác.
///
/// Điều kiện sở hữu nằm NGAY TRONG câu truy vấn: không tìm thấy ⇒ 404, không bao giờ 403.
/// Trả 403 là xác nhận id đó có tồn tại, tức là một kênh dò dữ liệu.
/// </summary>
public static class GamificationAccess
{
    public static Task<OfferingContext?> OfferingForInstructorAsync(
        IAppDbContext db, Guid offeringId, Guid instructorId, CancellationToken ct) =>
        (from o in db.CourseOfferings.AsNoTracking()
         where o.Id == offeringId
         join s in db.Subjects on o.SubjectId equals s.Id
         where s.InstructorId == instructorId
         select new OfferingContext(o.Id, s.InstructorId, o.TimeZoneId)).SingleOrDefaultAsync(ct);

    /// <summary>Enrollment của <paramref name="userId"/> trong lớp đó, hoặc null nếu không học lớp này.</summary>
    public static Task<LearnerContext?> LearnerAsync(
        IAppDbContext db, Guid offeringId, Guid userId, CancellationToken ct) =>
        (from e in db.Enrollments.AsNoTracking()
         where e.OfferingId == offeringId && e.UserId == userId
         join o in db.CourseOfferings on e.OfferingId equals o.Id
         join s in db.Subjects on o.SubjectId equals s.Id
         select new LearnerContext(e.Id, o.Id, s.InstructorId, o.TimeZoneId)).SingleOrDefaultAsync(ct);

    /// <summary>Mọi enrollment còn hiệu lực của lớp. Đã rút môn thì không lên bảng xếp hạng.</summary>
    public static IQueryable<Enrollment> ActiveEnrollments(IAppDbContext db, Guid offeringId) =>
        db.Enrollments.Where(e => e.OfferingId == offeringId && e.Status != EnrollmentStatus.Dropped);
}
