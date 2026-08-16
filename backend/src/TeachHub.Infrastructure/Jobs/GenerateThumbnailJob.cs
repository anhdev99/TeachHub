using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using TeachHub.Application.Common;
using TeachHub.Domain.Files;
using TeachHub.Infrastructure.Images;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure.Jobs;

/// <summary>
/// Dựng ảnh xem trước cho tệp ảnh sinh viên nộp (nợ Phase 3 Task 3.5 bước 4).
///
/// KHÔNG khai <c>[Queue]</c> nên chạy trên hàng đợi mặc định. Cố ý: hàng đợi "zip" chỉ có ít
/// worker để một file nén độc hại không chiếm hết (ADR-005), và đổ vài trăm tấm ảnh vào đó là
/// tự tay làm nghẽn đúng cái hàng đợi hẹp ấy.
///
/// Phần xử lý ảnh nằm ở <see cref="ThumbnailRenderer"/> để test được mà không cần database.
/// Job này chỉ còn đọc–ghi và quyết định xử lý lỗi.
/// </summary>
public sealed class GenerateThumbnailJob(
    AppDbContext db, IFileStorage storage, ILogger<GenerateThumbnailJob> log)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(Guid submissionFileId, CancellationToken ct)
    {
        var file = await db.SubmissionFiles
            .SingleOrDefaultAsync(f => f.Id == submissionFileId, ct);

        if (file is null) return;                              // đã bị xóa trong lúc chờ
        if (file.FilePath is null) return;                     // nộp bằng link, không có gì để đọc
        if (file.ThumbnailPath is not null) return;            // đã có rồi — retry không dựng lại
        if (!ThumbnailPolicy.IsImage(file.FileName)) return;   // không phải ảnh

        try
        {
            // Mở stream HAI LẦN: một lần cho header, một lần để giải mã. Stream của S3 không
            // tua lại được, và đệm cả tệp 200 MB vào RAM để tua là chiếm 200 MB bộ nhớ máy chủ
            // cho đúng một bài nộp.
            ThumbnailOutcome kq;
            await using (var head = await storage.OpenReadAsync(file.FilePath, ct))
            await using (var noiDung = await storage.OpenReadAsync(file.FilePath, ct))
                kq = await ThumbnailRenderer.RenderAsync(head, noiDung, ct);

            if (kq.Content is null)
            {
                // Từ chối có lý do rõ ràng (định dạng lạ, ảnh quá lớn): ghi log rồi dừng, KHÔNG
                // ném — không có lần chạy lại nào làm tấm ảnh đó nhỏ đi.
                log.LogWarning(
                    "Bỏ qua ảnh xem trước của tệp nộp {Id}: {LyDo}", file.Id, kq.Rejection);
                return;
            }

            // Thư mục riêng: xóa bài nộp thì xóa được cả ảnh xem trước mà không đụng file gốc,
            // và ảnh xem trước không bao giờ lẫn vào danh sách tệp sinh viên đã nộp.
            using var ms = new MemoryStream(kq.Content, writable: false);

            // ⚠️ Hai tham số dưới đây KHÔNG được dùng nguyên văn, và đừng suy ra đường dẫn từ
            // chúng. `Storage.Sanitize()` nuốt dấu `/`, nên tên container thành `thumbnails<guid>`
            // liền một mạch chứ không lồng thư mục; và tên tệp truyền vào bị thay bằng một
            // UUIDv7 mới, nên `{file.Id}.jpg` chỉ còn tác dụng gợi ý phần mở rộng.
            //
            // Cả hai adapter (`LocalFileStorage`, `S3FileStorage`) dùng chung một hàm `Sanitize`
            // nên chúng nhất quán với nhau — đây không phải lỗi. Ghi ra đây vì ý đồ "tên tệp
            // tất định" đọc từ dòng này là ý đồ chết: đường dẫn thật chỉ biết được qua
            // `luu.Path`, và đó là lý do nó được ghi xuống `ThumbnailPath` ngay bên dưới thay
            // vì dựng lại bằng chuỗi ở chỗ khác.
            var luu = await storage.SaveAsync(
                $"thumbnails/{file.SubmissionId}", $"{file.Id}.jpg", ms, "image/jpeg", ct);

            file.ThumbnailPath = luu.Path;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is UnknownImageFormatException
                                    or InvalidImageContentException
                                    or ImageFormatException
                                    or InvalidMemoryOperationException)
        {
            // Lỗi MONG ĐỢI: ảnh hỏng, định dạng không đọc được, hoặc nở quá trần bộ nhớ. Ghi
            // nhận rồi đi tiếp, KHÔNG ném lại — ném lại khiến Hangfire retry một tệp vốn dĩ
            // không bao giờ xử lý được, và mỗi vòng retry lại nạp đúng cái ảnh độc hại đó.
            //
            // Mọi exception khác cứ để ném ra: storage hỏng tạm thời là sự cố có thể tự khỏi,
            // và retry đúng là việc phải làm.
            log.LogWarning(e, "Không dựng được ảnh xem trước cho tệp nộp {Id}.", file.Id);
        }
    }
}
