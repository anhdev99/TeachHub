using Shouldly;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Tests;

public class GradeSnapshotBuilderTests
{
    private static readonly Guid CotThuongKy = Guid.CreateVersion7();
    private static readonly Guid CotCuoiMon = Guid.CreateVersion7();

    private static GradebookDto So(params GradeRowDto[] hang) => new(
        Guid.CreateVersion7(), "DA24TIN01", "LTW", "Lập trình Web",
        [
            new GradeColumnDto(CotThuongKy, "thuong_ky", "Thường kỳ", 40m, 0,
                AggregationKind.Average, 0, true, []),
            new GradeColumnDto(CotCuoiMon, "cuoi_mon", "Cuối môn", 60m, 1,
                AggregationKind.WeightedAverage, 0, true, []),
        ],
        [.. hang],
        [new GradeConditionDto("cuoi_mon", ConditionOperator.LessThan, 4m, CapAction.CapFinalAt, 3.9m)],
        BonusTarget.GradeColumn, CotThuongKy, 1m,
        WeightsValid: true, WeightsError: null, UnknownConditionColumnKeys: []);

    private static GradeRowDto Hang(string mssv, string ten, decimal tong) => new(
        Guid.CreateVersion7(), mssv, ten, "DA24TIN",
        [
            new GradeCellDto(CotThuongKy, 8m, 8.5m, IsManualOverride: false, Bonus: 0.5m),
            new GradeCellDto(CotCuoiMon, 7m, 7m, IsManualOverride: true, Bonus: 0m),
        ],
        BonusToFinal: 0m, FinalScore: tong);

    [Fact]
    public void Chuoi_JSON_khong_doi_khi_thu_tu_hang_tra_ve_khac_nhau()
    {
        // Thứ tự sắp xếp mặc định của Postgres phụ thuộc collation của database. Nếu bản chốt
        // giữ nguyên thứ tự truy vấn trả về thì cùng một bảng điểm trên hai máy chủ cho hai
        // hash khác nhau, và bản chốt mất sạch giá trị đối chiếu.
        var a = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m), Hang("2251012002", "Trần Bảo", 8m))));

        var b = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            So(Hang("2251012002", "Trần Bảo", 8m), Hang("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        // EnrollmentId sinh mới mỗi lần gọi Hang() nên hai chuỗi không giống hệt nhau; điều
        // cần khẳng định là THỨ TỰ MSSV đã được chuẩn hóa, không phải thứ tự truyền vào.
        a.IndexOf("2251012001", StringComparison.Ordinal)
            .ShouldBeLessThan(a.IndexOf("2251012002", StringComparison.Ordinal));
        b.IndexOf("2251012001", StringComparison.Ordinal)
            .ShouldBeLessThan(b.IndexOf("2251012002", StringComparison.Ordinal));
    }

    [Fact]
    public void Cung_mot_bang_diem_cho_cung_mot_chuoi()
    {
        var so = So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m));

        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(so))
            .ShouldBe(GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(so)));
    }

    [Fact]
    public void Payload_ghi_khoa_cot_chu_khong_ghi_id()
    {
        // Mẫu xuất và điều kiện điểm sàn nối vào cột bằng KEY. Bản chốt cũng phải vậy: id
        // không nói lên điều gì khi đọc lại sáu tháng sau, và cột có thể đã bị xóa.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        json.ShouldContain("thuong_ky");
        json.ShouldContain("cuoi_mon");
        json.ShouldNotContain(CotThuongKy.ToString());
    }

    [Fact]
    public void Payload_giu_nguyen_tieng_Viet_co_dau()
    {
        // Bản chốt còn phải đọc được bằng mắt lúc sinh viên khiếu nại. Escape thành \uXXXX
        // là biến cả trang thành ký tự thoát.
        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
                So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m))))
            .ShouldContain("Nguyễn Văn Ánh");
    }

    [Fact]
    public void Enum_ghi_bang_CHUOI_chu_khong_bang_so()
    {
        // Số thứ tự đổi nghĩa ngay khi ai đó chèn một giá trị vào giữa enum, và lúc đó mọi
        // bản chốt cũ nói sai về chính nó mà không có cách nào phát hiện.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        json.ShouldContain("average");
        json.ShouldContain("less_than");
    }

    [Fact]
    public void Diem_giu_nguyen_kieu_decimal_khong_qua_dau_phay_dong()
    {
        // 8.5 phải ra "8.5", không phải "8.4999999999999996". Điểm là decimal từ đầu tới cuối.
        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
                So(Hang("2251012001", "Nguyễn Văn Ánh", 7.6m))))
            .ShouldContain("8.5");
    }

    [Fact]
    public void Bang_diem_khong_co_sinh_vien_van_chot_duoc()
    {
        // Lớp đóng khi chưa ai ghi danh là ca hiếm nhưng có thật (lớp bị huỷ). Ném ở đây
        // nghĩa là giảng viên không đóng nổi lớp và không hiểu vì sao.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(So()));

        json.ShouldContain("\"rows\":[]");
    }
}
