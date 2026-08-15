namespace TeachHub.Application.Common;

/// <summary>
/// Cổng đẩy việc xử lý tệp nộp sang nền. Là interface ở tầng Application vì Hangfire nằm ở
/// Infrastructure: gọi thẳng <c>BackgroundJob.Enqueue</c> trong handler sẽ kéo Hangfire vào
/// tầng này và làm mọi test handler phải dựng một máy chủ job.
/// </summary>
public interface ISubmissionFileQueue
{
    /// <summary>Đẩy việc giải nén + kiểm cấu trúc lên hàng đợi riêng cho ZIP (ADR-005).</summary>
    void EnqueueProcessing(Guid submissionFileId);
}
