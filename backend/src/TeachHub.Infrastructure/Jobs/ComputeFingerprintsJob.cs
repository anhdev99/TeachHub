using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeachHub.Application.Common;
using TeachHub.Application.Features.Integrity;

namespace TeachHub.Infrastructure.Jobs;

/// <summary>Đẩy job qua Hangfire. Hiện thực của cổng <see cref="ISimilarityQueue"/>.</summary>
public sealed class HangfireSimilarityQueue(IBackgroundJobClient jobs) : ISimilarityQueue
{
    public void EnqueueFingerprinting(Guid submissionFileId) =>
        jobs.Enqueue<ComputeFingerprintsJob>(j => j.RunAsync(submissionFileId, CancellationToken.None));
}

/// <summary>
/// Phương án dự phòng khi Hangfire tắt (integration test, môi trường tối giản). Cùng khuôn với
/// <see cref="InlineSubmissionFileQueue"/>: chạy trên MỘT SCOPE DI RIÊNG, vì scope của request
/// đã bị dispose lúc job chạy và dùng lại DbContext của nó sẽ ném ObjectDisposedException ở
/// một chỗ không ai ngờ tới.
/// </summary>
public sealed class InlineSimilarityQueue(
    IServiceScopeFactory scopes, ILogger<InlineSimilarityQueue> log) : ISimilarityQueue
{
    public void EnqueueFingerprinting(Guid submissionFileId) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ComputeFingerprintsJob>();
                await job.RunAsync(submissionFileId, CancellationToken.None);
            }
            catch (Exception e)
            {
                // Không ai await Task này, nên exception không bắt ở đây biến mất hoàn toàn —
                // bài nộp không có dấu vân tay và báo cáo trùng lặp lặng lẽ bỏ qua nó.
                log.LogError(e, "Tính dấu vân tay cho tệp nộp {Id} thất bại.", submissionFileId);
            }
        });
}

/// <summary>
/// Vỏ Hangfire mỏng: toàn bộ thuật toán nằm ở <see cref="ComputeFingerprintsHandler"/> tầng
/// Application, nên test được bằng cách <c>new</c> handler mà không phải dựng máy chủ job.
/// Đi qua <c>ISender</c> cũng để hưởng luôn <c>TransactionBehavior</c> — xóa dấu vân tay cũ
/// và ghi dấu vân tay mới phải cùng sống hoặc cùng chết.
/// </summary>
public sealed class ComputeFingerprintsJob(ISender sender, ILogger<ComputeFingerprintsJob> log)
{
    /// <summary>
    /// Chạy trên queue "zip" cùng lý do với việc giải nén (ADR-005): băm một file nén 30 MB
    /// tốn vài giây CPU, và nó không được phép chiếm hết worker của mọi job khác.
    /// </summary>
    [Queue(JobsDependencyInjection.ZipQueue)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(Guid submissionFileId, CancellationToken ct)
    {
        var kq = await sender.Send(new ComputeFingerprintsCommand(submissionFileId), ct);

        // NotFound là ca MONG ĐỢI: tệp đã bị xóa trong lúc chờ trên hàng đợi. Ghi nhận rồi đi
        // tiếp, KHÔNG ném — ném lại khiến Hangfire retry một việc không bao giờ thành công.
        if (!kq.IsSuccess)
            log.LogWarning("Bỏ qua tính dấu vân tay cho tệp {Id}: {Reason}", submissionFileId, kq.Error);
    }
}
