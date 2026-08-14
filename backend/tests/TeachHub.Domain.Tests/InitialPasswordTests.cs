using Shouldly;
using TeachHub.Domain.Identity;

namespace TeachHub.Domain.Tests;

public class InitialPasswordTests
{
    [Fact]
    public void Khong_bao_gio_suy_ra_duoc_tu_MSSV()
    {
        // ĐÂY LÀ LÝ DO LỚP NÀY TỒN TẠI. Bản đầu tiên sinh mật khẩu "Th@{MSSV}", mà MSSV nằm
        // ngay trong danh sách lớp ai cũng đọc được — sinh viên đăng nhập được vào tài khoản
        // bạn cùng lớp TRƯỚC lần đổi mật khẩu đầu, rồi tự đặt mật khẩu mới. Cờ
        // MustChangePassword không cứu được: nó chỉ chặn SAU khi đã đăng nhập thành công.
        const string mssv = "2251012001";

        var mk = InitialPassword.Generate();

        mk.ShouldNotContain(mssv);
        mk.ShouldNotBe($"Th@{mssv}");
    }

    [Fact]
    public void Hai_lan_sinh_ra_hai_mat_khau_khac_nhau()
    {
        var tap = Enumerable.Range(0, 500).Select(_ => InitialPassword.Generate()).ToHashSet();
        tap.Count.ShouldBe(500);
    }

    [Fact]
    public void Du_dai_de_khong_do_duoc_bang_cach_thu()
    {
        InitialPassword.Generate().Length.ShouldBeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void Thoa_yeu_cau_do_manh_cua_Identity()
    {
        // Identity cấu hình RequiredLength = 8, RequireNonAlphanumeric = false, nhưng mặc định
        // vẫn đòi có chữ hoa, chữ thường và chữ số. Sinh ra mật khẩu bị chính UserManager
        // từ chối thì tính năng "tạo tài khoản cả lớp" hỏng câm.
        for (var i = 0; i < 200; i++)
        {
            var mk = InitialPassword.Generate();
            mk.ShouldContain(c => char.IsUpper(c));
            mk.ShouldContain(c => char.IsLower(c));
            mk.ShouldContain(c => char.IsDigit(c));
        }
    }

    [Fact]
    public void Khong_chua_ky_tu_de_doc_nham_khi_giang_vien_doc_to()
    {
        // Cùng lý do với JoinCode: giảng viên sẽ đọc mật khẩu này cho sinh viên gõ theo.
        var mau = string.Concat(Enumerable.Range(0, 300).Select(_ => InitialPassword.Generate()));

        mau.ShouldNotContain("O");
        mau.ShouldNotContain("l");
        mau.ShouldNotContain("I");
        mau.ShouldNotContain("0");
        mau.ShouldNotContain("1");
    }
}
