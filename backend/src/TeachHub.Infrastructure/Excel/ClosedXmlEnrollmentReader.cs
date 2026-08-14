using ClosedXML.Excel;
using TeachHub.Application.Common;
using TeachHub.Application.Features.Enrollments;

namespace TeachHub.Infrastructure.Excel;

public sealed class ClosedXmlEnrollmentReader : IEnrollmentReader
{
    /// <summary>
    /// Nhận diện cột theo TÊN HEADER chứ không theo vị trí: mẫu file của phòng đào tạo
    /// hay đổi thứ tự cột, và một lần đổi là hỏng toàn bộ import nếu bám theo chỉ số.
    /// </summary>
    private static readonly Dictionary<string, string[]> HeaderAliases = new()
    {
        ["StudentCode"] = ["mssv", "mã sv", "ma sv", "mã sinh viên", "ma sinh vien", "student code", "msv", "mã số sinh viên"],
        ["FullName"] = ["họ tên", "ho ten", "họ và tên", "ho va ten", "full name", "tên sv", "ten sv", "họ và tên đệm"],
        ["Email"] = ["email", "e-mail", "thư điện tử", "thu dien tu"],
        ["ClassName"] = ["lớp", "lop", "class", "lớp học"],
    };

    public RosterReadResult Read(Stream content)
    {
        using var wb = new XLWorkbook(content);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) return new RosterReadResult([], [], "File không có sheet nào.");

        var used = ws.RangeUsed();
        if (used is null) return new RosterReadResult([], [], "Sheet đầu tiên không có dữ liệu.");

        var headerRow = used.FirstRow();
        var headers = headerRow.Cells().Select(c => c.GetString().Trim()).ToList();

        var cot = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
        {
            var chuanHoa = headers[i].ToLowerInvariant();
            foreach (var (khoa, aliases) in HeaderAliases)
                if (!cot.ContainsKey(khoa) && aliases.Any(a => chuanHoa == a || chuanHoa.Contains(a, StringComparison.Ordinal)))
                    cot[khoa] = i + 1;   // ClosedXML đánh số cột từ 1
        }

        if (!cot.ContainsKey("StudentCode") || !cot.ContainsKey("FullName"))
            return new RosterReadResult([], headers,
                $"Không tìm thấy cột MSSV và/hoặc Họ tên. Các cột đọc được: {string.Join(", ", headers)}.");

        var rows = new List<ImportRow>();
        foreach (var row in used.Rows().Skip(1))
        {
            string Doc(string khoa) =>
                cot.TryGetValue(khoa, out var i) ? row.Cell(i).GetString() : "";

            rows.Add(new ImportRow(
                Doc("StudentCode"), Doc("FullName"), Doc("Email"),
                cot.ContainsKey("ClassName") ? Doc("ClassName") : null));
        }

        return new RosterReadResult(rows, headers, null);
    }
}
