using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Queries.GetMyProgress;

public sealed record MyLessonDto(
    Guid Id, string Title, int OrderIndex, LessonContentType ContentType, string? FileUrl,
    bool Completed, LessonAccess Access, string? LockedReason, int ComponentCount);

public sealed record MyChapterDto(
    Guid Id, string Title, string? Objective, int OrderIndex,
    UnlockRule UnlockRule, decimal CompletionPercent, List<MyLessonDto> Lessons);

public sealed record MyProgressDto(
    Guid OfferingId, string SubjectCode, string SubjectName, string ClassCode,
    decimal OverallPercent, List<MyChapterDto> Chapters);

public sealed record GetMyProgressQuery(Guid OfferingId, Guid UserId) : IQuery<Result<MyProgressDto>>;

public sealed class GetMyProgressQueryHandler(IAppDbContext db)
    : IQueryHandler<GetMyProgressQuery, Result<MyProgressDto>>
{
    public async Task<Result<MyProgressDto>> Handle(GetMyProgressQuery request, CancellationToken ct)
    {
        var lop = await (from e in db.Enrollments.AsNoTracking()
                         where e.OfferingId == request.OfferingId && e.UserId == request.UserId
                         join o in db.CourseOfferings on e.OfferingId equals o.Id
                         join s in db.Subjects on o.SubjectId equals s.Id
                         select new { EnrollmentId = e.Id, o.SubjectVersionId, o.ClassCode, s.Code, s.Name })
                        .SingleOrDefaultAsync(ct);

        // Không học lớp này ⇒ 404, không bao giờ 403.
        if (lop is null) return Result<MyProgressDto>.NotFound("Không tìm thấy lớp học phần.");

        // Nạp phẳng rồi ghép trong bộ nhớ: 3 truy vấn thay vì Include lồng nhau sinh N+1.
        var chapters = await db.Chapters.AsNoTracking()
            .Where(c => c.SubjectVersionId == lop.SubjectVersionId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        var chapterIds = chapters.Select(c => c.Id).ToList();
        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => chapterIds.Contains(l.ChapterId))
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(ct);

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var components = await db.LessonComponents.AsNoTracking()
            .Where(cp => lessonIds.Contains(cp.LessonId))
            .Select(cp => new { cp.Id, cp.LessonId, cp.IsRequired })
            .ToListAsync(ct);

        var lessonXong = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == lop.EnrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonId).ToListAsync(ct);

        var componentXong = await db.ComponentProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == lop.EnrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonComponentId).ToListAsync(ct);

        var progress = Progress.With(lessonXong, componentXong);

        var ketQua = new List<MyChapterDto>();
        var tongBai = 0;
        var tongXong = 0;

        foreach (var ch in chapters)
        {
            var baiCuaChuong = lessons.Where(l => l.ChapterId == ch.Id).OrderBy(l => l.OrderIndex).ToList();

            var node = new ChapterNode(ch.Id, ch.UnlockRule, [.. baiCuaChuong.Select(l =>
                new LessonNode(l.Id, l.Title, l.IsRequiredToUnlockNext,
                    [.. components.Where(cp => cp.LessonId == l.Id)
                                  .Select(cp => new ComponentNode(cp.Id, cp.IsRequired))]))]);

            var access = UnlockEngine.Evaluate(node, progress);

            ketQua.Add(new MyChapterDto(
                ch.Id, ch.Title, ch.Objective, ch.OrderIndex, ch.UnlockRule,
                UnlockEngine.CompletionPercent(node, progress),
                [.. baiCuaChuong.Select(l =>
                {
                    var truyCap = access.GetValueOrDefault(l.Id, LessonAccess.Unlocked);

                    // Nêu đích danh bài đang chặn. Chỉ hiện một ổ khóa câm là nguồn tin nhắn
                    // hỏi bài số một của mọi LMS.
                    var chan = truyCap == LessonAccess.Locked
                        ? UnlockEngine.BlockingLesson(node, l.Id, progress)
                        : null;

                    return new MyLessonDto(
                        l.Id, l.Title, l.OrderIndex, l.ContentType, l.FileUrl,
                        progress.CompletedLessons.Contains(l.Id), truyCap,
                        chan is null ? null : $"Hoàn thành “{chan.Title}” để mở bài này.",
                        components.Count(cp => cp.LessonId == l.Id));
                })]));

            tongBai += node.Lessons.Count;
            tongXong += node.Lessons.Count(l => UnlockEngine.IsComplete(l, progress));
        }

        var tongPhanTram = tongBai == 0 ? 0m : Math.Round(tongXong * 100m / tongBai, 1);

        return Result<MyProgressDto>.Ok(new MyProgressDto(
            request.OfferingId, lop.Code, lop.Name, lop.ClassCode, tongPhanTram, ketQua));
    }
}
