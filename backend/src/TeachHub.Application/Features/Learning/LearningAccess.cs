using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Learning;

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

    /// <summary>
    /// Bài học chứa <paramref name="componentId"/> đã mở khoá với người học chưa?
    /// </summary>
    /// <remarks>
    /// Có ghi danh KHÔNG có nghĩa là được chạm vào mọi bài. `UnlockEngine` mở dần theo tiến độ,
    /// và nếu phép kiểm đó chỉ sống ở truy vấn dựng trang học thì nó chỉ là một ổ khoá VẼ TRÊN
    /// GIAO DIỆN: mọi endpoint khác nhận id bài học vẫn phục vụ bình thường.
    ///
    /// Dựng lại cây chương rồi gọi chính `UnlockEngine.Evaluate` mà trang học dùng — cố ý không
    /// viết một phép kiểm "gọn hơn" cho riêng chỗ này. Hai cách tính mở khoá song song là bảo
    /// đảm sẽ có ngày chúng trả lời khác nhau, và bên lỏng hơn mới là bên quyết định.
    /// </remarks>
    public static async Task<bool> IsComponentUnlockedAsync(
        IAppDbContext db, Guid componentId, Guid enrollmentId, CancellationToken ct)
    {
        var viTri = await db.LessonComponents.AsNoTracking()
            .Where(cp => cp.Id == componentId)
            .Join(db.Lessons, cp => cp.LessonId, l => l.Id, (cp, l) => new { l.Id, l.ChapterId })
            .SingleOrDefaultAsync(ct);

        if (viTri is null) return false;

        var chuong = await db.Chapters.AsNoTracking()
            .Where(c => c.Id == viTri.ChapterId)
            .Select(c => new { c.Id, c.UnlockRule })
            .SingleAsync(ct);

        var bai = await db.Lessons.AsNoTracking()
            .Where(l => l.ChapterId == viTri.ChapterId)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new { l.Id, l.Title, l.IsRequiredToUnlockNext })
            .ToListAsync(ct);

        var phan = await db.LessonComponents.AsNoTracking()
            .Where(cp => bai.Select(b => b.Id).Contains(cp.LessonId))
            .Select(cp => new { cp.Id, cp.LessonId, cp.IsRequired })
            .ToListAsync(ct);

        var lessonXong = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonId).ToListAsync(ct);

        var componentXong = await db.ComponentProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonComponentId).ToListAsync(ct);

        var node = new ChapterNode(chuong.Id, chuong.UnlockRule, [.. bai.Select(l =>
            new LessonNode(l.Id, l.Title, l.IsRequiredToUnlockNext,
                [.. phan.Where(cp => cp.LessonId == l.Id)
                        .Select(cp => new ComponentNode(cp.Id, cp.IsRequired))]))]);

        var access = UnlockEngine.Evaluate(node, Progress.With(lessonXong, componentXong));
        return access.GetValueOrDefault(viTri.Id, LessonAccess.Unlocked) != LessonAccess.Locked;
    }
}
