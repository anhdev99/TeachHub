using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;

namespace TeachHub.Application.Features.Content;

/// <summary>
/// Lần ngược từ một bản ghi nội dung về giảng viên sở hữu nó. Gom về một chỗ vì mọi handler
/// của Phase 1 đều cần đúng phép kiểm này, và mỗi bản sao chép thủ công là một chỗ có thể quên
/// điều kiện <c>instructor_id</c> — mà quên một chỗ là lộ nội dung của lớp người khác.
///
/// Luôn dùng dạng "có tồn tại bản ghi thoả CẢ id LẪN chủ sở hữu" để điều kiện nằm trong
/// câu truy vấn, không nạp rồi lọc.
/// </summary>
public static class ContentOwnership
{
    public static Task<bool> OwnsVersionAsync(IAppDbContext db, Guid versionId, Guid instructorId, CancellationToken ct) =>
        db.SubjectVersions.AnyAsync(v => v.Id == versionId &&
            db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId), ct);

    public static Task<bool> OwnsChapterAsync(IAppDbContext db, Guid chapterId, Guid instructorId, CancellationToken ct) =>
        db.Chapters.AnyAsync(c => c.Id == chapterId &&
            db.SubjectVersions.Any(v => v.Id == c.SubjectVersionId &&
                db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId)), ct);

    public static Task<bool> OwnsLessonAsync(IAppDbContext db, Guid lessonId, Guid instructorId, CancellationToken ct) =>
        db.Lessons.AnyAsync(l => l.Id == lessonId &&
            db.Chapters.Any(c => c.Id == l.ChapterId &&
                db.SubjectVersions.Any(v => v.Id == c.SubjectVersionId &&
                    db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId))), ct);

    public static Task<bool> OwnsComponentAsync(IAppDbContext db, Guid componentId, Guid instructorId, CancellationToken ct) =>
        db.LessonComponents.AnyAsync(cp => cp.Id == componentId &&
            db.Lessons.Any(l => l.Id == cp.LessonId &&
                db.Chapters.Any(c => c.Id == l.ChapterId &&
                    db.SubjectVersions.Any(v => v.Id == c.SubjectVersionId &&
                        db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId)))), ct);
}
