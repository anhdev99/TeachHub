using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Subjects;

/// <summary>
/// Nhân bản toàn bộ nội dung của một SubjectVersion sang một bản draft mới (ADR-004).
///
/// Điểm mấu chốt: LessonComponent.RefId trỏ tới Assignment/Quiz. Nếu copy nguyên giá trị
/// cũ, bản clone sẽ dùng chung Assignment với bản gốc — sửa bài tập ở năm mới sẽ đổi luôn
/// bài tập của lớp năm cũ. Vì vậy phải clone Assignment/Quiz TRƯỚC, dựng bảng ánh xạ
/// id cũ → id mới, rồi mới remap RefId.
///
/// Cố ý nạp bằng 5 câu truy vấn phẳng rồi ghép trong bộ nhớ, không Include lồng nhau:
/// một môn 15 chương × 6 bài × 3 component là ~270 component, Include lồng sẽ thành N+1.
/// </summary>
public sealed class SubjectVersionCloner(IAppDbContext db, TimeProvider clock)
{
    public async Task<SubjectVersion> CloneAsync(Guid sourceVersionId, string newLabel, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var source = await db.SubjectVersions.AsNoTracking()
            .SingleAsync(v => v.Id == sourceVersionId, ct);

        var chapters = await db.Chapters.AsNoTracking()
            .Where(c => c.SubjectVersionId == sourceVersionId)
            .OrderBy(c => c.OrderIndex).ToListAsync(ct);

        var chapterIds = chapters.Select(c => c.Id).ToList();
        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => chapterIds.Contains(l.ChapterId))
            .OrderBy(l => l.OrderIndex).ToListAsync(ct);

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var components = await db.LessonComponents.AsNoTracking()
            .Where(c => lessonIds.Contains(c.LessonId))
            .OrderBy(c => c.OrderIndex).ToListAsync(ct);

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.SubjectVersionId == sourceVersionId).ToListAsync(ct);
        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(q => q.SubjectVersionId == sourceVersionId).ToListAsync(ct);

        var target = SubjectVersion.CreateDraft(source.SubjectId, newLabel, now, clonedFrom: source.Id);
        target.Syllabus = source.Syllabus.DeepCopy();
        target.Changelog = source.Changelog;
        db.SubjectVersions.Add(target);

        // id cũ → id mới, cho MỌI bản ghi được nhân bản
        var map = new Dictionary<Guid, Guid>();

        // 1) Assignment và Quiz phải clone TRƯỚC — component sẽ tham chiếu tới chúng.
        foreach (var a in assignments)
        {
            var copy = a.CloneTo(target.Id);
            map[a.Id] = copy.Id;
            db.Assignments.Add(copy);
        }
        foreach (var q in quizzes)
        {
            var copy = q.CloneTo(target.Id);
            map[q.Id] = copy.Id;
            db.Quizzes.Add(copy);
        }

        // 2) Chương → bài → component, remap FK theo bảng ánh xạ.
        foreach (var ch in chapters)
        {
            var chCopy = new Chapter
            {
                SubjectVersionId = target.Id,
                Title = ch.Title,
                OrderIndex = ch.OrderIndex,
                Objective = ch.Objective,
                FoundationTags = [.. ch.FoundationTags],
                UnlockRule = ch.UnlockRule,
            };
            map[ch.Id] = chCopy.Id;
            db.Chapters.Add(chCopy);

            foreach (var ls in lessons.Where(l => l.ChapterId == ch.Id))
            {
                var lsCopy = new Lesson
                {
                    ChapterId = chCopy.Id,
                    Title = ls.Title,
                    OrderIndex = ls.OrderIndex,
                    ContentType = ls.ContentType,
                    FileUrl = ls.FileUrl,          // dùng chung file đã upload — cố ý,
                    TextContent = ls.TextContent,  // nhân bản file PPT/PDF là lãng phí vô nghĩa
                    DurationMinutes = ls.DurationMinutes,
                    IsRequiredToUnlockNext = ls.IsRequiredToUnlockNext,
                };
                map[ls.Id] = lsCopy.Id;
                db.Lessons.Add(lsCopy);

                foreach (var cp in components.Where(c => c.LessonId == ls.Id))
                {
                    db.LessonComponents.Add(new LessonComponent
                    {
                        LessonId = lsCopy.Id,
                        Type = cp.Type,
                        OrderIndex = cp.OrderIndex,
                        IsRequired = cp.IsRequired,
                        Config = cp.Config.DeepCopy(),

                        // ★ Dòng quan trọng nhất của cả phase.
                        // Ném lỗi chứ không âm thầm gán null: một component trỏ ra ngoài version
                        // nguồn nghĩa là dữ liệu đã sai từ trước. Gán null sẽ tạo ra một bài học
                        // có "component bài tập" nhưng không có bài tập nào — lỗi câm chỉ lộ khi
                        // sinh viên mở bài và thấy trang trắng.
                        RefId = cp.RefId is null ? null
                              : map.TryGetValue(cp.RefId.Value, out var moi) ? moi
                              : throw new InvalidOperationException(
                                  $"Component {cp.Id} trỏ tới {cp.RefId} nhưng bản ghi đó không thuộc " +
                                  $"version nguồn {sourceVersionId}. Dữ liệu không nhất quán, dừng clone."),
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return target;
    }
}
