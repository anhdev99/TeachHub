using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TeachHub.Domain.Content;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Filters;

/// <summary>Loại id nằm trong route, để filter biết lần ngược lên SubjectVersion bằng đường nào.</summary>
public enum RouteIdKind { Version, Chapter, Lesson, Component }

/// <summary>
/// Chặn mọi thao tác sửa nội dung khi SubjectVersion không còn ở trạng thái draft.
///
/// GẮN XUỐNG TỪNG ACTION SỬA NỘI DUNG, tuyệt đối không gắn ở cấp class. Cùng những controller
/// này còn phải chứa các action GHI CỦA SINH VIÊN mà Phase 2 thêm vào
/// (POST /api/lessons/{id}/complete) — chúng chạy trên lớp đang dạy, tức trên một
/// SubjectVersion đã published. Gắn filter ở cấp class thì mọi sinh viên đánh dấu hoàn thành
/// bài học đều nhận 409 và tính năng mở khóa tuần tự của Phase 2 chết hoàn toàn — lỗi chỉ lộ
/// khi ghép hai phase lại, mỗi phase xét riêng đều "đúng".
///
/// Cũng vì thế KHÔNG dùng cơ chế [SkipDraftCheck]: danh sách loại trừ luôn thiếu một cái, và
/// cái thiếu đó là một tính năng chết âm thầm. Danh sách bao gồm rõ ràng hơn danh sách loại trừ.
///
/// Vì sao action filter chứ không phải pipeline behavior của MediatR: thứ cần kiểm là THAM SỐ
/// ROUTE, không phải một trường có sẵn trên command. Behavior chỉ nhìn thấy request object nên
/// muốn dùng được thì mọi command phải tự khai một interface và nhớ cài nó — lại quay về
/// "nhớ thì đúng, quên thì sai".
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireDraftVersionAttribute(RouteIdKind kind, string routeKey = "id")
    : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<Filter>(services, kind, routeKey);

    private sealed class Filter(AppDbContext db, RouteIdKind kind, string routeKey) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var ct = ctx.HttpContext.RequestAborted;

            if (!Guid.TryParse(ctx.RouteData.Values[routeKey]?.ToString(), out var id))
            {
                ctx.Result = new NotFoundResult();
                return;
            }

            var status = await ResolveStatusAsync(id, ct);

            // Không tìm thấy ⇒ 404. Filter cố ý KHÔNG kiểm quyền sở hữu — đó là việc của handler,
            // và handler cũng trả 404 cho tài nguyên của người khác nên hai lớp nhất quán.
            if (status is null)
            {
                ctx.Result = new NotFoundResult();
                return;
            }

            if (status != SubjectVersionStatus.Draft)
            {
                ctx.Result = new ConflictObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Phiên bản không sửa được",
                    Detail = $"Phiên bản đang ở trạng thái {status}, chỉ bản nháp mới sửa được nội dung.",
                });
                return;
            }

            await next();
        }

        private Task<SubjectVersionStatus?> ResolveStatusAsync(Guid id, CancellationToken ct) => kind switch
        {
            RouteIdKind.Version => db.SubjectVersions.AsNoTracking()
                .Where(v => v.Id == id).Select(v => (SubjectVersionStatus?)v.Status).SingleOrDefaultAsync(ct),

            RouteIdKind.Chapter => db.Chapters.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => db.SubjectVersions.Where(v => v.Id == c.SubjectVersionId)
                    .Select(v => (SubjectVersionStatus?)v.Status).SingleOrDefault())
                .SingleOrDefaultAsync(ct),

            RouteIdKind.Lesson => db.Lessons.AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => db.Chapters.Where(c => c.Id == l.ChapterId)
                    .Select(c => db.SubjectVersions.Where(v => v.Id == c.SubjectVersionId)
                        .Select(v => (SubjectVersionStatus?)v.Status).SingleOrDefault())
                    .SingleOrDefault())
                .SingleOrDefaultAsync(ct),

            RouteIdKind.Component => db.LessonComponents.AsNoTracking()
                .Where(cp => cp.Id == id)
                .Select(cp => db.Lessons.Where(l => l.Id == cp.LessonId)
                    .Select(l => db.Chapters.Where(c => c.Id == l.ChapterId)
                        .Select(c => db.SubjectVersions.Where(v => v.Id == c.SubjectVersionId)
                            .Select(v => (SubjectVersionStatus?)v.Status).SingleOrDefault())
                        .SingleOrDefault())
                    .SingleOrDefault())
                .SingleOrDefaultAsync(ct),

            _ => throw new InvalidOperationException($"Loại id chưa xử lý: {kind}"),
        };
    }
}
