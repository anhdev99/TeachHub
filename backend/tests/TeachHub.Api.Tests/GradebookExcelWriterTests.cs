using ClosedXML.Excel;
using Shouldly;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Grading;
using TeachHub.Infrastructure.Excel;

namespace TeachHub.Api.Tests;

/// <summary>
/// Test thuần cho bộ dựng .xlsx — KHÔNG dùng container. Bộ dựng không chạm database, và những
/// thứ dễ sai nhất ở đây (MSSV bị đổi thành số khoa học, điểm ghi thành chuỗi) chỉ nhìn thấy
/// khi mở lại chính file nhị phân sinh ra.
/// </summary>
public class GradebookExcelWriterTests
{
    private static readonly Guid CotThuongKy = Guid.CreateVersion7();
    private static readonly Guid CotCuoiMon = Guid.CreateVersion7();

    private static readonly ClosedXmlGradebookWriter Writer = new();

    private static GradebookDto So(params GradeRowDto[] hang) => new(
        Guid.CreateVersion7(), "DA24TIN01", "LTW", "Lập trình Web",
        [
            new GradeColumnDto(CotThuongKy, "thuong_ky", "Thường kỳ", 40m, 0,
                AggregationKind.Average, 0, true, []),
            new GradeColumnDto(CotCuoiMon, "cuoi_mon", "Cuối môn", 60m, 1,
                AggregationKind.WeightedAverage, 0, true, []),
        ],
        [.. hang], [],
        BonusTarget.GradeColumn, null, null,
        WeightsValid: true, WeightsError: null, UnknownConditionColumnKeys: []);

    private static GradeRowDto Hang(string mssv, string ten, decimal? thuongKy, decimal tong) => new(
        Guid.CreateVersion7(), mssv, ten, "DA24TIN",
        thuongKy is null
            ? []
            : [new GradeCellDto(CotThuongKy, thuongKy.Value, thuongKy.Value, false, 0m)],
        BonusToFinal: 0m, FinalScore: tong);

    private static List<GradeExportItemDto> Mau(int soLe = 1) =>
    [
        new(GradeExportSource.StudentCode, null, "MSSV", 0, 0),
        new(GradeExportSource.FullName, null, "Họ và tên", 1, 0),
        new(GradeExportSource.GradeColumn, "thuong_ky", "Điểm quá trình", 2, soLe),
        new(GradeExportSource.FinalScore, null, "Điểm tổng kết", 3, soLe),
    ];

    private static IXLWorksheet Mo(ExportedFile file)
    {
        var wb = new XLWorkbook(new MemoryStream(file.Content));
        return wb.Worksheet(1);
    }

    [Fact]
    public void File_mo_duoc_va_dung_so_dong()
    {
        var file = Writer.Write(
            So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m), Hang("2251012002", "Trần Bảo", 9m, 8m)),
            Mau());

        file.FileName.ShouldBe("bang-diem-DA24TIN01.xlsx");
        file.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        Mo(file).LastRowUsed()!.RowNumber().ShouldBe(3);   // 1 tiêu đề + 2 sinh viên
    }

    [Fact]
    public void Mssv_giu_nguyen_dang_chuoi_khong_bi_doi_thanh_so()
    {
        // MSSV bắt đầu bằng 0 hoặc dài 10 chữ số bị Excel biến thành "2.25101E+09".
        // Bảng điểm nộp cho phòng đào tạo mà MSSV thành số khoa học là hỏng việc.
        var o = Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m)), Mau())).Cell(2, 1);

        o.DataType.ShouldBe(XLDataType.Text);
        o.GetString().ShouldBe("2251012001");
    }

    [Fact]
    public void Diem_la_SO_THAT_trong_o_chu_khong_phai_chuoi()
    {
        // Đây là toàn bộ lý do bỏ CSV để sang .xlsx. Ghi chuỗi thì mọi công thức
        // SUM/AVERAGE/lọc của phòng đào tạo trả 0 mà không báo lỗi nào.
        var ws = Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m)), Mau()));

        ws.Cell(2, 3).DataType.ShouldBe(XLDataType.Number);
        ws.Cell(2, 3).GetDouble().ShouldBe(8d);
        ws.Cell(2, 4).DataType.ShouldBe(XLDataType.Number);
        ws.Cell(2, 4).GetDouble().ShouldBe(7.6d, 0.0001d);
    }

    [Fact]
    public void So_chu_so_thap_phan_theo_cau_hinh_cua_mau()
    {
        // Làm tròn là chuyện HIỂN THỊ: giá trị lưu vẫn đầy đủ, chỉ NumberFormat đổi.
        Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.65m)), Mau(soLe: 2)))
            .Cell(2, 4).Style.NumberFormat.Format.ShouldBe("0.00");
    }

    [Fact]
    public void Tieu_de_in_dam_va_dong_dau_bi_dong_bang()
    {
        var ws = Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m)), Mau()));

        ws.Cell(1, 1).Style.Font.Bold.ShouldBeTrue();
        ws.Cell(1, 3).GetString().ShouldBe("Điểm quá trình");   // tiêu đề lấy từ mẫu, không phải tên cột
        ws.SheetView.SplitRow.ShouldBe(1);
    }

    [Fact]
    public void Ten_tieng_Viet_co_dau_hien_thi_dung()
    {
        Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m)), Mau()))
            .Cell(2, 2).GetString().ShouldBe("Nguyễn Văn Ánh");
    }

    [Fact]
    public void Chua_cham_thi_o_de_TRONG_chu_khong_ghi_so_khong()
    {
        // AVERAGE bỏ qua ô trống nhưng ĐẾM ô có số 0. Ghi 0 cho bài chưa chấm làm điểm trung
        // bình lớp thấp đi một cách âm thầm, và không ai đối chiếu lại con số đó.
        var o = Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", null, 0m)), Mau())).Cell(2, 3);

        o.IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void Do_rong_cot_khong_de_mac_dinh()
    {
        // Cột "Họ và tên" rộng mặc định 8.43 ký tự thì mọi tên tiếng Việt đều bị cắt, và
        // người dùng phải kéo tay từng cột sau mỗi lần xuất.
        Mo(Writer.Write(So(Hang("2251012001", "Nguyễn Văn Ánh", 8m, 7.6m)), Mau()))
            .Column(2).Width.ShouldBeGreaterThanOrEqualTo(26d);
    }
}
