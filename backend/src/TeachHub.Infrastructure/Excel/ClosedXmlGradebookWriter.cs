using ClosedXML.Excel;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Grading;

namespace TeachHub.Infrastructure.Excel;

/// <summary>
/// Dựng file .xlsx cho bảng điểm theo mẫu xuất tự cấu hình. Hiện thực của cổng
/// <see cref="IGradebookExcelWriter"/> — ClosedXML chỉ được phép sống ở tầng này.
///
/// Mẫu xuất nối vào cột điểm bằng KEY chứ không bằng id (xem <see cref="GradeExportTemplate"/>),
/// và toàn bộ phép quy đổi ô đi qua <see cref="GradeExportValues"/> để bản .xlsx và bản CSV
/// không bao giờ cho ra hai con số khác nhau cho cùng một lớp.
/// </summary>
public sealed class ClosedXmlGradebookWriter : IGradebookExcelWriter
{
    private const string SheetName = "Bảng điểm";

    public ExportedFile Write(GradebookDto so, IReadOnlyList<GradeExportItemDto> cot)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(SheetName);

        for (var c = 0; c < cot.Count; c++)
        {
            var o = ws.Cell(1, c + 1);
            o.Value = cot[c].Header;
            o.Style.Font.Bold = true;
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.WrapText = true;

            ws.Column(c + 1).Width = DoRong(cot[c]);

            // MSSV ép định dạng Text ở cấp CỘT. Excel tự đoán kiểu khi người dùng gõ đè lên
            // ô: "2251012001" gõ vào một cột định dạng General biến thành 2.25101E+09, và
            // bảng điểm nộp cho phòng đào tạo hỏng ngay ở cột đầu tiên.
            if (cot[c].Source == GradeExportSource.StudentCode)
                ws.Column(c + 1).Style.NumberFormat.Format = "@";
        }

        for (var r = 0; r < so.Rows.Count; r++)
        {
            var hang = so.Rows[r];

            for (var c = 0; c < cot.Count; c++)
            {
                var muc = cot[c];
                var o = ws.Cell(r + 2, c + 1);

                if (!GradeExportValues.IsNumeric(muc.Source))
                {
                    o.Value = GradeExportValues.Text(hang, muc);
                    continue;
                }

                var diem = GradeExportValues.Number(so, hang, muc);

                // Chưa có điểm thì để TRỐNG chứ không ghi 0: AVERAGE bỏ qua ô trống nhưng đếm
                // ô có số 0, nên ghi 0 làm điểm trung bình lớp thấp đi một cách âm thầm.
                if (diem is null) continue;

                // Ghi số THẬT vào ô, không phải chuỗi đã định dạng sẵn. Phòng đào tạo cộng,
                // lọc và sắp xếp trên cột này; với chuỗi thì mọi công thức của họ trả 0 mà
                // không báo lỗi nào. Số chữ số thập phân là chuyện HIỂN THỊ, đặt bằng
                // NumberFormat chứ không bằng cách làm tròn giá trị lưu.
                o.Value = diem.Value;
                o.Style.NumberFormat.Format = DinhDangSo(muc.DecimalPlaces);
                o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        // Đóng băng dòng tiêu đề: lớp 300 sinh viên cuộn tới hàng 250 mà không còn thấy tên
        // cột thì người dùng phải đếm cột bằng tay và sẽ nhập nhầm ô.
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        return new ExportedFile(
            ms.ToArray(),
            $"bang-diem-{so.ClassCode}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    /// <summary>
    /// Độ rộng cố định theo loại cột thay vì <c>AdjustToContents</c>: đo bề rộng chữ cần tới
    /// bảng metric font của máy chủ, mà máy chủ Linux trong container thường không có font
    /// nào — kết quả là mọi cột co về một độ rộng vô nghĩa. Vẫn nới theo độ dài tiêu đề để
    /// tiêu đề dài do giảng viên tự đặt không bị cắt.
    /// </summary>
    private static double DoRong(GradeExportItemDto muc)
    {
        var toiThieu = muc.Source switch
        {
            GradeExportSource.StudentCode => 14d,
            GradeExportSource.FullName => 26d,
            GradeExportSource.Email => 28d,
            GradeExportSource.ClassName => 14d,
            _ => 12d,
        };

        return Math.Max(toiThieu, Math.Min(muc.Header.Length + 2d, 40d));
    }

    private static string DinhDangSo(int soLe) =>
        soLe <= 0 ? "0" : "0." + new string('0', Math.Min(soLe, 3));
}
