using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Files;

namespace TeachHub.Application.Features.Content.Commands.UploadLessonFile;

public sealed record LessonFileDto(Guid LessonId, string FileUrl, long SizeBytes, string ContentType);

/// <summary>
/// Nhận <c>Stream</c> + tên file + độ dài, KHÔNG nhận <c>IFormFile</c>: IFormFile là kiểu của
/// ASP.NET Core, đưa nó xuống Application là kéo cả tầng web vào tầng nghiệp vụ và biến mọi
/// test handler thành bài dựng HTTP giả. Với Stream thì test chỉ cần một MemoryStream.
/// </summary>
public sealed record UploadLessonFileCommand(string FileName, long Length, Stream Content)
    : ICommand<Result<LessonFileDto>>
{
    public Guid LessonId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UploadLessonFileCommandHandler(IAppDbContext db, IFileStorage storage)
    : ICommandHandler<UploadLessonFileCommand, Result<LessonFileDto>>
{
    private const long GioiHanByte = 200L * 1024 * 1024;

    public async Task<Result<LessonFileDto>> Handle(UploadLessonFileCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsLessonAsync(db, request.LessonId, request.InstructorId, ct))
            return Result<LessonFileDto>.NotFound("Không tìm thấy bài học.");

        if (request.Length > GioiHanByte)
            return Result<LessonFileDto>.PayloadTooLarge("Tệp vượt quá 200 MB.");

        // Đọc đúng vài byte đầu để kiểm chữ ký thật, không nạp cả file vào bộ nhớ.
        var head = new byte[FileSignatureValidator.HeaderBytes];
        var doc = await request.Content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);

        if (!FileSignatureValidator.Matches(request.FileName, head.AsSpan(0, doc)))
            return Result<LessonFileDto>.Invalid(
                "Tệp không đúng định dạng bài giảng cho phép (PDF, PPTX, DOCX, PNG, JPG).");

        // Ghép lại phần đã đọc với phần còn lại: stream gốc đã bị tiêu thụ mất mấy byte đầu.
        var noiDung = new MemoryStream();
        await noiDung.WriteAsync(head.AsMemory(0, doc), ct);
        await request.Content.CopyToAsync(noiDung, ct);
        noiDung.Position = 0;

        var saved = await storage.SaveAsync("lessons", request.FileName, noiDung,
            SuyContentType(request.FileName), ct);

        var lesson = await db.Lessons.SingleAsync(l => l.Id == request.LessonId, ct);
        lesson.FileUrl = saved.Path;
        lesson.ContentType = SuyLoaiNoiDung(request.FileName);
        await db.SaveChangesAsync(ct);

        return Result<LessonFileDto>.Ok(
            new LessonFileDto(lesson.Id, saved.Path, saved.SizeBytes, saved.ContentType));
    }

    private static string SuyContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    private static LessonContentType SuyLoaiNoiDung(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => LessonContentType.Pdf,
            ".pptx" => LessonContentType.Ppt,
            _ => LessonContentType.Interactive,
        };
}
