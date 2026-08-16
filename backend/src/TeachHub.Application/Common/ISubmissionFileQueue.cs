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

    /// <summary>
    /// Đẩy việc dựng ảnh xem trước cho một tệp ảnh, trên hàng đợi MẶC ĐỊNH — không phải hàng
    /// đợi "zip". Hàng đợi zip cố ý chỉ có ít worker để một file nén độc hại không chiếm hết;
    /// nhét thêm việc tạo thumbnail vào đó là tự tay lấp đầy đúng cái hàng đợi hẹp ấy, và bài
    /// nộp .zip của cả lớp phải xếp hàng sau vài trăm tấm ảnh.
    /// </summary>
    void EnqueueThumbnail(Guid submissionFileId);
}
