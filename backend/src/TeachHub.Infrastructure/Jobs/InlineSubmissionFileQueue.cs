using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeachHub.Application.Common;

namespace TeachHub.Infrastructure.Jobs;

/// <summary>
/// Phương án dự phòng khi Hangfire tắt: chạy job trên một scope DI riêng, ngay sau khi
/// request hiện tại trả về. Không await trong luồng request — mục tiêu NFR "upload phản hồi
/// dưới 2 giây" vẫn phải giữ.
///
/// Scope riêng là bắt buộc: scope của request đã bị dispose lúc job chạy, dùng lại DbContext
/// của nó sẽ ném ObjectDisposedException ở một chỗ không ai ngờ tới.
/// </summary>
public sealed class InlineSubmissionFileQueue(
    IServiceScopeFactory scopes, ILogger<InlineSubmissionFileQueue> log) : ISubmissionFileQueue
{
    public void EnqueueProcessing(Guid submissionFileId) =>
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ProcessSubmissionFileJob>();
                await job.RunAsync(submissionFileId, CancellationToken.None);
            }
            catch (Exception e)
            {
                // Không có ai await Task này, nên exception không bắt ở đây sẽ biến mất
                // hoàn toàn — tệp kẹt ở Pending mà log không có một dòng nào.
                log.LogError(e, "Xử lý tệp nộp {Id} thất bại.", submissionFileId);
            }
        });
}
