using TeachHub.Application.Features.Enrollments;

namespace TeachHub.Application.Common;

/// <summary>
/// Kết quả đọc file danh sách. <c>Headers</c> giữ lại tên cột đọc được để báo lỗi hữu ích:
/// "không tìm thấy cột MSSV, các cột đọc được là …" thay vì một lỗi mơ hồ.
/// </summary>
public sealed record RosterReadResult(List<ImportRow> Rows, List<string> Headers, string? Error);

/// <summary>
/// Cổng đọc file danh sách sinh viên. Application không biết ClosedXML tồn tại — nhờ vậy
/// test handler chỉ cần một implementation giả, không cần dựng file .xlsx.
/// </summary>
public interface IEnrollmentReader
{
    RosterReadResult Read(Stream content);
}
