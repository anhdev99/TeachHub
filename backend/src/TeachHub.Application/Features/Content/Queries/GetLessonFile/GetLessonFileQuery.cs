using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Learning;

namespace TeachHub.Application.Features.Content.Queries.GetLessonFile;

public sealed record LessonFileStream(Stream Content, string ContentType, string FileName);

/// <summary>
/// Phục vụ file bài giảng cho trình duyệt xem trước. Đây là món nợ kỹ thuật từ Phase 1:
/// <c>LocalFileStorage.GetPresignedUrlAsync</c> trả đường dẫn tới endpoint này, nhưng endpoint
/// chỉ làm được ở Phase 2 — trước đó chưa có khái niệm "người học của lớp" để kiểm quyền cho đúng.
/// </summary>
public sealed record GetLessonFileQuery(string Path, Guid UserId) : IQuery<Result<LessonFileStream>>;

public sealed class GetLessonFileQueryHandler(IAppDbContext db, IFileStorage storage)
    : IQueryHandler<GetLessonFileQuery, Result<LessonFileStream>>
{
    public async Task<Result<LessonFileStream>> Handle(GetLessonFileQuery request, CancellationToken ct)
    {
        // Đi từ ĐƯỜNG DẪN ngược về bài học sở hữu nó. Không có bài nào trỏ tới file này nghĩa là
        // nó không thuộc hệ thống — trả 404 chứ không mở file ra.
        var lesson = await db.Lessons.AsNoTracking()
            .Where(l => l.FileUrl == request.Path)
            .Select(l => new { l.Id })
            .SingleOrDefaultAsync(ct);

        if (lesson is null) return Result<LessonFileStream>.NotFound("Không tìm thấy tệp.");

        // Hai đường được phép: giảng viên sở hữu nội dung, hoặc sinh viên đang học lớp
        // ghim đúng phiên bản chứa bài này.
        var laGiangVien = await ContentOwnership.OwnsLessonAsync(db, lesson.Id, request.UserId, ct);
        var laNguoiHoc = laGiangVien
            || await LearningAccess.EnrollmentForLessonAsync(db, lesson.Id, request.UserId, ct) is not null;

        if (!laNguoiHoc) return Result<LessonFileStream>.NotFound("Không tìm thấy tệp.");

        try
        {
            var stream = await storage.OpenReadAsync(request.Path, ct);
            return Result<LessonFileStream>.Ok(
                new LessonFileStream(stream, SuyContentType(request.Path), System.IO.Path.GetFileName(request.Path)));
        }
        catch (FileNotFoundException)
        {
            return Result<LessonFileStream>.NotFound("Không tìm thấy tệp.");
        }
        catch (UnauthorizedAccessException)
        {
            // Đường dẫn cố thoát khỏi thư mục lưu trữ. Trả 404 chứ không 403: đừng xác nhận
            // rằng có gì đó ở ngoài kia để dò.
            return Result<LessonFileStream>.NotFound("Không tìm thấy tệp.");
        }
    }

    private static string SuyContentType(string path) =>
        System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };
}
