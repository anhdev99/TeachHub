namespace TeachHub.Application.Common;

/// <summary>
/// Cổng đẩy việc tính dấu vân tay mã nguồn sang nền. Cùng lý do với
/// <see cref="ISubmissionFileQueue"/>: Hangfire nằm ở Infrastructure, gọi thẳng
/// <c>BackgroundJob.Enqueue</c> trong handler sẽ kéo nó vào tầng Application và làm mọi test
/// handler phải dựng một máy chủ job.
///
/// Phải chạy ở nền chứ không chạy trong luồng request: một file nén 40 MB chứa 300 tệp mã
/// nguồn mất vài giây để băm, và NFR "upload phản hồi dưới 2 giây" không cho phép chờ.
/// </summary>
public interface ISimilarityQueue
{
    /// <summary>
    /// Đẩy việc băm một tệp nộp. Gọi SAU khi giải nén xong (<c>ProcessSubmissionFileJob</c>),
    /// vì với file .zip thì cây thư mục chính là danh sách tệp cần băm — chạy trước thì
    /// <c>FileTree</c> còn null và job không biết bên trong có gì.
    /// </summary>
    void EnqueueFingerprinting(Guid submissionFileId);
}
