using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;

namespace TeachHub.Application.Features.Learning;

/// <summary>
/// Sinh viên chỉ được chạm vào nội dung của phiên bản mà LỚP MÌNH HỌC đang ghim. Gom phép kiểm
/// về một chỗ: mỗi bản sao chép thủ công là một chỗ có thể quên, và quên một chỗ là sinh viên
/// lớp này đánh dấu hoàn thành bài của lớp khác.
/// </summary>
public static class LearningAccess
{
    /// <summary>Enrollment của <paramref name="userId"/> ứng với bài học đó, hoặc null nếu không có quyền.</summary>
    public static Task<Guid?> EnrollmentForLessonAsync(
        IAppDbContext db, Guid lessonId, Guid userId, CancellationToken ct) =>
        (from l in db.Lessons.AsNoTracking()
         where l.Id == lessonId
         join c in db.Chapters on l.ChapterId equals c.Id
         join o in db.CourseOfferings on c.SubjectVersionId equals o.SubjectVersionId
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select (Guid?)e.Id).FirstOrDefaultAsync(ct);

    public static Task<Guid?> EnrollmentForComponentAsync(
        IAppDbContext db, Guid componentId, Guid userId, CancellationToken ct) =>
        (from cp in db.LessonComponents.AsNoTracking()
         where cp.Id == componentId
         join l in db.Lessons on cp.LessonId equals l.Id
         join c in db.Chapters on l.ChapterId equals c.Id
         join o in db.CourseOfferings on c.SubjectVersionId equals o.SubjectVersionId
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select (Guid?)e.Id).FirstOrDefaultAsync(ct);
}
