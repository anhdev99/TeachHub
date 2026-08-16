using System.Security.Cryptography;
using System.Text;

namespace TeachHub.Domain.Grading;

/// <summary>
/// Băm nội dung bản chốt sổ điểm. Hàm thuần: cùng chuỗi vào thì cùng chuỗi ra, trên mọi máy
/// và mọi lần chạy — đó là toàn bộ giá trị của <see cref="GradeSnapshot.PayloadHash"/>.
/// </summary>
public static class GradeSnapshotHash
{
    /// <summary>
    /// SHA-256 dạng hex CHỮ THƯỜNG, đúng 64 ký tự.
    ///
    /// Encoding ghim cứng UTF-8 chứ không dùng mặc định của máy chủ: bảng điểm đầy tên tiếng
    /// Việt có dấu, và băm theo encoding của máy thì cùng một bản chốt cho hai hash khác nhau
    /// trên hai máy chủ — bản chốt mất sạch giá trị đối chiếu mà không ai phát hiện.
    /// </summary>
    public static string Compute(string payloadJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
}
