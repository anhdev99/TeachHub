using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Learning;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Learning;

/// <summary>
/// Lớp đã đóng thì mọi đường GHI của sinh viên phải đóng theo.
///
/// <c>OfferingStatus</c> có ba giá trị từ Phase 2, nhưng cho tới trước lần sửa này chỗ DUY NHẤT
/// đọc tới nó là <see cref="CourseOffering.AcceptsJoinCode"/>. Nghĩa là
/// <c>POST /api/offerings/{id}/close</c> đổi một cái nhãn và chốt một bản sổ điểm, rồi sinh viên
/// vẫn nộp bài, vẫn làm trắc nghiệm, vẫn đánh dấu tiến độ như thường — và bản chốt "tại thời
/// điểm đóng" đối chiếu với một sổ điểm còn tiếp tục thay đổi sau lưng nó.
///
/// Ba quyết định đi kèm, mỗi cái đều cố ý:
///
/// 1. <b>409 chứ không 404.</b> Khác hẳn ca không-thuộc-về-mình: ở đây sinh viên biết rõ lớp này
///    có thật và mình có học. Giấu đi bằng 404 chỉ khiến họ tưởng hệ thống hỏng và nhắn tin hỏi
///    giảng viên. Kèm <c>detail</c> tiếng Việt đọc được, vì đó là thứ duy nhất giao diện hiển thị.
///
/// 2. <b>Chỉ khoá đường GHI.</b> Sinh viên vẫn phải xem lại được bài học, điểm, bài đã nộp và gợi
///    ý đã mở sau khi lớp kết thúc — khoá đọc là xoá sổ toàn bộ giá trị của việc học.
///
/// 3. <b>KHÔNG khoá giảng viên.</b> Chấm bài thường diễn ra SAU khi lớp kết thúc; chặn giảng viên
///    chấm nghĩa là không ai dám đóng lớp và tính năng thành vô dụng.
///
/// Gom về một chỗ vì lý do y hệt <see cref="LearningAccess"/>: mỗi bản sao chép thủ công là một
/// chỗ có thể quên, và quên một chỗ là cái khoá không tồn tại.
/// </summary>
public static class OfferingLock
{
    /// <summary>
    /// Thông điệp trả cho người dùng. Nhận động từ của thao tác để câu tiếng Việt đọc được —
    /// "không thao tác được nữa" là loại thông báo khiến người đọc phải đoán họ vừa làm gì sai.
    /// </summary>
    public static string ThongDiep(string hanhDong) =>
        $"Lớp học phần đã kết thúc nên không {hanhDong} được nữa.";

    /// <summary>Lớp <paramref name="offeringId"/> đã đóng chưa.</summary>
    public static Task<bool> DaDongAsync(IAppDbContext db, Guid offeringId, CancellationToken ct) =>
        db.CourseOfferings.AsNoTracking()
            .AnyAsync(o => o.Id == offeringId && o.Status == OfferingStatus.Closed, ct);

    /// <summary>
    /// Lớp mà <paramref name="enrollmentId"/> thuộc về đã đóng chưa. Bản dùng nhiều nhất: hầu hết
    /// handler phía sinh viên đã phải giải ra enrollment để kiểm quyền, nên không tốn thêm gì.
    /// </summary>
    public static async Task<bool> DaDongTheoGhiDanhAsync(
        IAppDbContext db, Guid enrollmentId, CancellationToken ct)
    {
        var trangThai = await (from e in db.Enrollments.AsNoTracking()
                               where e.Id == enrollmentId
                               join o in db.CourseOfferings on e.OfferingId equals o.Id
                               select (OfferingStatus?)o.Status).FirstOrDefaultAsync(ct);

        // Ép kiểu nullable là cố ý: `FirstOrDefaultAsync` trên enum trần trả về `Draft` khi không
        // có hàng nào, tức "chưa đóng" — một câu trả lời vừa sai vừa im lặng nếu enrollment biến mất.
        return trangThai == OfferingStatus.Closed;
    }
}

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
