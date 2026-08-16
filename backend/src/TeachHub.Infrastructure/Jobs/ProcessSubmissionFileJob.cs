using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeachHub.Application.Common;
using TeachHub.Domain.Files;
using TeachHub.Domain.Submissions;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Infrastructure.Jobs;

/// <summary>Đẩy job qua Hangfire. Hiện thực của cổng <see cref="ISubmissionFileQueue"/>.</summary>
public sealed class HangfireSubmissionFileQueue(IBackgroundJobClient jobs) : ISubmissionFileQueue
{
    public void EnqueueProcessing(Guid submissionFileId) =>
        jobs.Enqueue<ProcessSubmissionFileJob>(j => j.RunAsync(submissionFileId, CancellationToken.None));
}

public sealed class ProcessSubmissionFileJob(
    AppDbContext db, IFileStorage storage, ISimilarityQueue similarity,
    ILogger<ProcessSubmissionFileJob> log)
{
    /// <summary>
    /// Chạy trên queue "zip" với ít worker (ADR-005): một file nén độc hại không được phép
    /// chiếm hết worker và làm treo mọi job khác.
    /// </summary>
    [Queue(JobsDependencyInjection.ZipQueue)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(Guid submissionFileId, CancellationToken ct)
    {
        var file = await db.SubmissionFiles
            .Include(f => f.Slot)
            .SingleOrDefaultAsync(f => f.Id == submissionFileId, ct);

        if (file is null) return;                            // đã bị xóa trong lúc chờ
        if (file.FilePath is null) return;                   // nộp bằng link, không có gì để giải nén

        if (!file.Slot.AcceptsZip)
        {
            file.ProcessingStatus = FileProcessingStatus.Done;
            await db.SaveChangesAsync(ct);
            return;
        }

        file.ProcessingStatus = FileProcessingStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            await using var stream = await storage.OpenReadAsync(file.FilePath, ct);
            var tree = SafeZipReader.ReadTree(stream, ZipLimits.Default);

            file.FileTree = tree;

            if (file.Slot.RequiredStructure is null)
            {
                file.StructureCheckPassed = null;
                file.MissingStructureItems = [];
            }
            else
            {
                var kq = tree.CheckStructure(file.Slot.RequiredStructure);
                file.StructureCheckPassed = kq.Passed;
                file.MissingStructureItems = kq.Missing;
            }

            file.ProcessingStatus = FileProcessingStatus.Done;
            file.ProcessingError = null;
        }
        catch (UnsafeArchiveException e)
        {
            // Lỗi MONG ĐỢI: file hỏng hoặc độc hại. Ghi nhận rồi đi tiếp, KHÔNG ném lại —
            // ném lại khiến Hangfire retry một file vốn dĩ không bao giờ xử lý được.
            // Mọi exception khác cứ để ném ra: đó có thể là sự cố tạm thời của storage,
            // và retry là đúng việc phải làm.
            log.LogWarning("Tệp nộp {Id} bị từ chối: {Reason}", file.Id, e.Message);
            file.ProcessingStatus = FileProcessingStatus.Failed;
            file.ProcessingError = e.Message;
        }

        await db.SaveChangesAsync(ct);

        // Băm dấu vân tay mã nguồn (Phase 6) chạy SAU khi giải nén xong, không phải trước:
        // với file .zip thì chính FileTree là danh sách tệp cần băm, nên gọi sớm hơn thì job
        // kia thấy FileTree còn null và không biết bên trong có gì.
        //
        // Chỉ đẩy khi giải nén thành công. Đẩy cả file hỏng thì job băm chạy rồi thất bại
        // hàng loạt trên đúng những tệp đã biết là không đọc được.
        if (file.ProcessingStatus == FileProcessingStatus.Done)
            similarity.EnqueueFingerprinting(file.Id);
    }
}
