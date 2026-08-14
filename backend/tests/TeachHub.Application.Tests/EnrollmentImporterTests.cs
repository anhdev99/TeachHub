using Shouldly;
using TeachHub.Application.Features.Enrollments;

namespace TeachHub.Application.Tests;

public class EnrollmentImporterTests
{
    private static ImportRow Row(string ma, string ten, string email) => new(ma, ten, email, null);

    [Fact]
    public void Nhap_thanh_cong_cac_dong_hop_le()
    {
        var kq = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
        ]);

        kq.Valid.Count.ShouldBe(2);
        kq.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Mssv_trung_trong_cung_file_bao_loi_dung_so_dong()
    {
        var kq = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
            Row("2251012001", "Nguyễn Văn A", "a2@sv.edu.vn"),   // dòng 4 trong Excel
        ]);

        kq.Valid.Count.ShouldBe(2);                    // 2 dòng tốt VẪN được nhập
        kq.Errors.Count.ShouldBe(1);
        kq.Errors[0].RowNumber.ShouldBe(4);            // 1-based + 1 dòng header
        kq.Errors[0].Message.ShouldContain("2251012001");
    }

    [Fact]
    public void Mssv_bi_excel_doc_thanh_so_van_duoc_chuan_hoa()
    {
        // Excel biến "2251012001" thành số rồi xuất ra "2251012001.0" tùy phiên bản.
        // Đây là lỗi phổ biến nhất khi import.
        var kq = EnrollmentImporter.Validate([Row("2251012001.0", "Nguyễn Văn A", "a@sv.edu.vn")]);

        kq.Valid.Single().StudentCode.ShouldBe("2251012001");
    }

    [Fact]
    public void Cat_khoang_trang_thua_va_ha_thap_email()
    {
        var kq = EnrollmentImporter.Validate([Row("  2251012001  ", " Nguyễn  Văn A ", "  A@SV.EDU.VN ")]);

        var sv = kq.Valid.Single();
        sv.StudentCode.ShouldBe("2251012001");
        sv.FullName.ShouldBe("Nguyễn Văn A");    // khoảng trắng giữa cũng gộp lại
        sv.Email.ShouldBe("a@sv.edu.vn");
    }

    [Fact]
    public void Bo_qua_dong_trong_hoan_toan_khong_bao_loi()
    {
        // File thật hay có vài dòng trống ở cuối do người dùng bôi đen thừa.
        var kq = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("", "", ""),
        ]);

        kq.Valid.Count.ShouldBe(1);
        kq.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Thieu_mssv_hoac_ho_ten_bao_loi_dong_do()
    {
        var kq = EnrollmentImporter.Validate([
            Row("", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "", "b@sv.edu.vn"),
        ]);

        kq.Errors.Count.ShouldBe(2);
        kq.Errors.Select(e => e.RowNumber).ShouldBe([2, 3]);
    }

    [Fact]
    public void Email_sai_dinh_dang_bao_loi_nhung_khong_chan_ca_file()
    {
        var kq = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "khong-phai-email"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
        ]);

        kq.Valid.Count.ShouldBe(1);
        kq.Errors.Single().RowNumber.ShouldBe(2);
    }

    [Fact]
    public void Thieu_email_van_nhap_duoc_vi_email_khong_bat_buoc()
    {
        // Danh sách của phòng đào tạo thường thiếu email; SV vẫn ghép được vào lớp bằng MSSV.
        var kq = EnrollmentImporter.Validate([Row("2251012001", "Nguyễn Văn A", "")]);

        kq.Errors.ShouldBeEmpty();
        kq.Valid.Single().Email.ShouldBe("");
    }

    [Fact]
    public void Kich_ban_nghiem_thu_40_dong_co_2_trung_va_1_thieu_ho_ten_thi_nhap_37()
    {
        var rows = Enumerable.Range(1, 40)
            .Select(i => Row($"225101{i:D4}", $"Sinh viên {i}", $"sv{i}@sv.edu.vn"))
            .ToList();

        rows[10] = Row("2251010001", "Trùng thứ nhất", "x1@sv.edu.vn");   // trùng dòng 1
        rows[20] = Row("2251010002", "Trùng thứ hai",  "x2@sv.edu.vn");   // trùng dòng 2
        rows[30] = Row("2251010099", "",               "x3@sv.edu.vn");   // thiếu họ tên

        var kq = EnrollmentImporter.Validate(rows);

        kq.Valid.Count.ShouldBe(37);
        kq.Errors.Count.ShouldBe(3);
        kq.Errors.Select(e => e.RowNumber).ShouldBe([12, 22, 32]);
    }
}
