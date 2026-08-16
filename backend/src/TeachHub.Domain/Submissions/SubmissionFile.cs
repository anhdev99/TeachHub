using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;
using TeachHub.Domain.Files;

namespace TeachHub.Domain.Submissions;

public enum FileProcessingStatus { Pending, Processing, Done, Failed }

/// <summary>
/// Một tệp (hoặc một đường link) sinh viên nộp vào một slot. Upload chỉ lưu file gốc rồi
/// trả về ngay; giải nén và dựng cây thư mục chạy ở job nền trên queue riêng (ADR-005).
/// </summary>
public class SubmissionFile : Entity
{
    public Guid SubmissionId { get; set; }
    public Guid SlotId { get; set; }
    public SubmissionSlot Slot { get; set; } = null!;

    /// <summary>
    /// Chép lại khóa slot tại thời điểm nộp. Trùng lặp có chủ ý: giảng viên đổi tên slot
    /// sau khi cả lớp đã nộp thì các bài cũ vẫn biết mình thuộc ô nào.
    /// </summary>
    public required string SlotKey { get; set; }

    public required string FileName { get; set; }

    /// <summary>Đường dẫn trong storage. Null khi sinh viên nộp bằng link.</summary>
    public string? FilePath { get; set; }

    public string? ExternalUrl { get; set; }
    public long SizeBytes { get; set; }
    public string? ContentType { get; set; }

    /// <summary>
    /// Đường dẫn ảnh xem trước trong storage. Null khi tệp không phải ảnh, hoặc khi job nền
    /// chưa chạy xong, hoặc khi ảnh hỏng không giải mã được — cả ba ca đều hiển thị giống
    /// nhau ở giao diện (không có ảnh xem trước), nên không cần cột trạng thái riêng.
    /// </summary>
    public string? ThumbnailPath { get; set; }

    public FileProcessingStatus ProcessingStatus { get; set; } = FileProcessingStatus.Pending;
    public string? ProcessingError { get; set; }

    /// <summary>Cây thư mục bên trong file nén — lưu jsonb, chỉ có với slot nhận .zip.</summary>
    public FileTree? FileTree { get; set; }

    /// <summary>
    /// null = slot không khai cấu trúc bắt buộc. false = thiếu, nhưng VẪN cho nộp
    /// (SRS UC-08 bước 4) — chỉ gắn cờ để giảng viên thấy.
    /// </summary>
    public bool? StructureCheckPassed { get; set; }

    public List<string> MissingStructureItems { get; set; } = [];

    public required DateTimeOffset UploadedAt { get; set; }

    public bool IsLink => ExternalUrl is not null;
}
