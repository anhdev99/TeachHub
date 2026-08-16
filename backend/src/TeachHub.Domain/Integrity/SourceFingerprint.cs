using TeachHub.Domain.Common;

namespace TeachHub.Domain.Integrity;

/// <summary>
/// Một dấu vân tay Winnowing của một tệp mã nguồn đã nộp (Gap #14).
///
/// Bảng này chỉ chứa số, không chứa mã nguồn. Đó là chủ ý: dấu vân tay không khôi phục ngược
/// ra mã được, nên kể cả khi bảng bị lộ thì bài của sinh viên vẫn không bị lộ.
///
/// Không lưu kèm <c>SubmissionId</c> hay <c>AssignmentId</c> dù truy vấn so sánh luôn cần cả
/// hai: mọi dữ liệu chép lại là một chỗ có thể lệch khi sinh viên nộp lại bài. Truy vấn join
/// qua <c>submission_files → submissions</c> — hai lần join trên khóa chính, rẻ hơn nhiều so
/// với một ngày đi tìm vì sao báo cáo trùng lặp hiện bài của bài tập khác.
/// </summary>
public class SourceFingerprint : Entity
{
    public required Guid SubmissionFileId { get; set; }

    /// <summary>
    /// Đường dẫn tệp bên trong file nén, null nếu sinh viên nộp thẳng một tệp mã nguồn.
    /// Thiếu trường này thì màn hình so sánh biết "hai bài trùng 80%" nhưng không chỉ ra được
    /// tệp nào bên trong file zip trùng với tệp nào — tức là giảng viên vẫn phải tự dò tay.
    /// </summary>
    public string? EntryPath { get; set; }

    /// <summary>Giá trị băm k-gram. <c>bigint</c> dưới DB, có index (Gap #14).</summary>
    public long Hash { get; set; }

    /// <summary>
    /// Vị trí (offset ký tự) của k-gram trong mã nguồn GỐC, không phải chỉ số token.
    ///
    /// Winnowing làm việc trên chuỗi token đã chuẩn hóa nên nó trả về chỉ số token; tầng ứng
    /// dụng quy đổi ngược về offset ký tự trước khi ghi xuống. Lưu chỉ số token thì màn hình
    /// so sánh cạnh nhau không tô sáng được gì cả: nó có mã nguồn thật trong tay, còn bảng
    /// token thì không ai lưu — muốn dựng lại phải chuẩn hóa lại chính xác cùng một phiên bản
    /// thuật toán, tức là màn hình hiển thị bị khóa cứng vào chi tiết của bộ tách token.
    /// </summary>
    public int Position { get; set; }

    /// <summary>Số ký tự k-gram này phủ trong mã nguồn gốc — đầu kia của đoạn cần tô sáng.</summary>
    public int Length { get; set; }
}
