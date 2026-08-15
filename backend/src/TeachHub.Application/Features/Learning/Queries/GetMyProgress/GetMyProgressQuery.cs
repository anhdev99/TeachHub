using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Application.Features.Learning.Queries.GetMyProgress;

/// <param name="Completed">
/// Bài đã HOÀN THÀNH theo đúng nghĩa mà phần trăm và luật mở khóa đang dùng
/// (<see cref="UnlockEngine.IsComplete"/>): đã xem xong nội dung VÀ xong mọi phần bắt buộc.
/// </param>
/// <param name="MarkedDone">
/// Sinh viên đã bấm "Đã học xong" cho phần trình bày. Tách khỏi <paramref name="Completed"/>
/// vì giao diện cần phản hồi cú bấm ngay cả khi bài chưa đủ điều kiện tính là hoàn thành.
/// </param>
/// <param name="RequiredComponentsRemaining">
/// Số phần bắt buộc còn thiếu. Không có con số này thì sinh viên bấm xong mà không thấy gì
/// đổi và cũng không biết còn thiếu gì — đúng ngõ cụt đã gặp khi chạy thử.
/// </param>
/// <param name="Components">
/// Danh sách phần của bài. Trước đây chỉ trả <paramref name="ComponentCount"/>, nên giao diện
/// ghi được "3 phần trong bài · còn 1 phần bắt buộc" mà KHÔNG hiện phần nào để làm: sinh viên
/// không có đường hoàn thành phần bắt buộc, và bài kế tiếp khóa vĩnh viễn. Con số một mình
/// không phải là một màn hình dùng được.
/// </param>
public sealed record MyLessonDto(
    Guid Id, string Title, int OrderIndex, LessonContentType ContentType, string? FileUrl,
    bool Completed, bool MarkedDone, int RequiredComponentsRemaining,
    LessonAccess Access, string? LockedReason, int ComponentCount,
    List<MyComponentDto> Components);

/// <param name="Html">Nội dung nhúng của hook / custom_note / code_along. Null với loại khác.</param>
public sealed record MyComponentDto(
    Guid Id, LessonComponentType Type, int OrderIndex, bool IsRequired, bool Completed,
    string? Html, string? ReflectionPrompt, string? StarterCodeUrl, string? Language,
    int InlineQuestionCount);

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
            .OrderBy(cp => cp.OrderIndex)
            .Select(cp => new
            {
                cp.Id, cp.LessonId, cp.IsRequired, cp.Type, cp.OrderIndex,
                cp.Config.Html, cp.Config.ReflectionPrompt,
                cp.Config.StarterCodeUrl, cp.Config.Language,
                InlineQuestionCount = cp.Config.InlineQuestions.Count,
            })
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
                    var nut = node.Lessons.Single(n => n.Id == l.Id);

                    // Nêu đích danh bài đang chặn. Chỉ hiện một ổ khóa câm là nguồn tin nhắn
                    // hỏi bài số một của mọi LMS.
                    var chan = truyCap == LessonAccess.Locked
                        ? UnlockEngine.BlockingLesson(node, l.Id, progress)
                        : null;

                    // Chốt chặn allowlist ở ĐƯỜNG ĐỌC, không chỉ ở giao diện: giao diện nhúng
                    // thẳng file_url của bài video_embed vào iframe. Lọc ở đây thì dù URL lọt
                    // vào DB bằng đường nào (endpoint sửa bài học thêm sau, import, sửa tay),
                    // sinh viên vẫn không bao giờ nhận được một URL ngoài danh sách cho phép.
                    var fileUrl = l.ContentType == LessonContentType.VideoEmbed
                                  && !VideoEmbedPolicy.IsAllowed(l.FileUrl)
                        ? null
                        : l.FileUrl;

                    // "Xong" phải dùng CÙNG một định nghĩa với phần trăm và luật mở khóa —
                    // tức UnlockEngine.IsComplete. Trước đây chỗ này lấy thẳng
                    // progress.CompletedLessons, nên giao diện hiện ✓ ở Bài 1 trong khi Bài 2
                    // vẫn khóa với lý do "Hoàn thành Bài 1": sinh viên không có đường nào đi tiếp.
                    return new MyLessonDto(
                        l.Id, l.Title, l.OrderIndex, l.ContentType, fileUrl,
                        UnlockEngine.IsComplete(nut, progress),
                        progress.CompletedLessons.Contains(l.Id),
                        nut.Components.Count(c => c.IsRequired
                                                  && !progress.CompletedComponents.Contains(c.Id)),
                        truyCap,
                        chan is null ? null : $"Hoàn thành “{chan.Title}” để mở bài này.",
                        components.Count(cp => cp.LessonId == l.Id),
                        [.. components
                            .Where(cp => cp.LessonId == l.Id)
                            .OrderBy(cp => cp.OrderIndex)
                            .Select(cp => new MyComponentDto(
                                cp.Id, cp.Type, cp.OrderIndex, cp.IsRequired,
                                progress.CompletedComponents.Contains(cp.Id),
                                cp.Html, cp.ReflectionPrompt, cp.StarterCodeUrl, cp.Language,
                                cp.InlineQuestionCount))]);
                })]));

            tongBai += node.Lessons.Count;
            tongXong += node.Lessons.Count(l => UnlockEngine.IsComplete(l, progress));
        }

        var tongPhanTram = tongBai == 0 ? 0m : Math.Round(tongXong * 100m / tongBai, 1);

        return Result<MyProgressDto>.Ok(new MyProgressDto(
            request.OfferingId, lop.Code, lop.Name, lop.ClassCode, tongPhanTram, ketQua));
    }
}
