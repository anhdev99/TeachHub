using System.Globalization;
using System.Text.RegularExpressions;

namespace TeachHub.Application.Features.Enrollments;

public sealed record ImportRow(string StudentCode, string FullName, string Email, string? ClassName);
public sealed record ImportError(int RowNumber, string Message);
public sealed record ValidatedStudent(string StudentCode, string FullName, string Email, string? ClassName);
public sealed record ImportResult(List<ValidatedStudent> Valid, List<ImportError> Errors);

public static partial class EnrollmentImporter
{
    /// <summary>
    /// Nhập được bao nhiêu nhập bấy nhiêu. Một dòng hỏng không được làm hỏng cả file —
    /// giảng viên chỉ cần sửa đúng những dòng được liệt kê rồi nhập bổ sung. Import thất bại
    /// toàn phần vì một dòng lỗi nghĩa là phải sửa file bằng tay trước mỗi lần thử.
    /// </summary>
    public static ImportResult Validate(IReadOnlyList<ImportRow> rows)
    {
        var valid = new List<ValidatedStudent>();
        var errors = new List<ImportError>();
        var daThay = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var soDong = i + 2;   // +1 vì 1-based, +1 vì dòng header
            var r = rows[i];

            var ma = NormalizeStudentCode(r.StudentCode);
            var ten = NormalizeName(r.FullName);
            var email = r.Email?.Trim().ToLowerInvariant() ?? "";

            if (ma.Length == 0 && ten.Length == 0 && email.Length == 0)
                continue;                                    // dòng trống: bỏ qua lặng lẽ

            if (ma.Length == 0) { errors.Add(new(soDong, "Thiếu MSSV.")); continue; }
            if (ten.Length == 0) { errors.Add(new(soDong, $"Thiếu họ tên (MSSV {ma}).")); continue; }

            // Email không bắt buộc: danh sách của phòng đào tạo thường thiếu, và SV vẫn
            // ghép được vào lớp bằng MSSV. Nhưng có thì phải đúng định dạng.
            if (email.Length > 0 && !EmailPattern().IsMatch(email))
            { errors.Add(new(soDong, $"Email không hợp lệ: '{r.Email}'.")); continue; }

            if (!daThay.Add(ma))
            { errors.Add(new(soDong, $"MSSV {ma} bị trùng với một dòng phía trên.")); continue; }

            valid.Add(new ValidatedStudent(ma, ten, email, r.ClassName?.Trim()));
        }

        return new ImportResult(valid, errors);
    }

    /// <summary>
    /// Excel hay biến MSSV thành số rồi xuất ra "2251012001.0" hoặc dạng khoa học.
    /// Khôi phục về chuỗi chữ số gốc.
    /// </summary>
    private static string NormalizeStudentCode(string? raw)
    {
        var s = raw?.Trim() ?? "";
        if (s.Length == 0) return "";

        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d == Math.Floor(d))
            return ((long)d).ToString(CultureInfo.InvariantCulture);

        return s;
    }

    private static string NormalizeName(string? raw) =>
        WhitespaceRuns().Replace(raw?.Trim() ?? "", " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
