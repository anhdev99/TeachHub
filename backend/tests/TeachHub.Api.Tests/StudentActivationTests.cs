using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

public class StudentActivationTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    private async Task<(Guid OfferingId, string JoinCode, string StudentCode, string Email)>
        SeedLopCoDanhSachAsync()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var maSv = $"22510{duoi}";
        var email = $"sv-{duoi}@sv.edu.vn";

        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Enrollments.Add(new Enrollment
            {
                OfferingId = offeringId, StudentCode = maSv, FullName = "Sinh viên thử", Email = email,
            });
            await db.SaveChangesAsync();
        }

        var o = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}");
        return (offeringId, o.GetProperty("join_code").GetString()!, maSv, email);
    }

    /// <summary>Tạo tài khoản sinh viên trực tiếp (không qua lời mời) rồi đăng nhập.</summary>
    private async Task<HttpClient> DangNhapSinhVienAsync(
        string email, string? studentCode, bool mustChange = false, string matKhau = "Sinh@123")
    {
        using (var scope = factory.CreateServiceScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            if (!await roles.RoleExistsAsync(Roles.Student))
                await roles.CreateAsync(new AppRole(Roles.Student));

            if (await users.FindByEmailAsync(email) is null)
            {
                var u = new AppUser
                {
                    UserName = email, Email = email, FullName = "Sinh viên thử",
                    StudentCode = studentCode, EmailConfirmed = true, MustChangePassword = mustChange,
                };
                await users.CreateAsync(u, matKhau);
                await users.AddToRoleAsync(u, Roles.Student);
            }
        }

        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = matKhau });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
        return client;
    }

    [Fact]
    public async Task Sv_nhap_ma_lop_dung_thi_duoc_gan_vao_enrollment_co_san()
    {
        var (offeringId, joinCode, maSv, email) = await SeedLopCoDanhSachAsync();
        var sv = await DangNhapSinhVienAsync(email, maSv);

        var res = await sv.PostAsJsonAsync("/api/offerings/join", new { join_code = joinCode });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var lop = await sv.GetFromJsonAsync<JsonElement>("/api/me/offerings");
        lop.EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ShouldContain(offeringId);
    }

    [Fact]
    public async Task Sv_khong_co_trong_danh_sach_lop_thi_bi_tu_choi()
    {
        // Mã lớp bị lan truyền ra ngoài là chuyện chắc chắn xảy ra. Mã đúng vẫn chưa đủ —
        // phải có tên trong danh sách do phòng đào tạo gửi.
        var (_, joinCode, _, _) = await SeedLopCoDanhSachAsync();
        var nguoiLa = await DangNhapSinhVienAsync("nguoi-la@sv.edu.vn", "9999999999");

        var res = await nguoiLa.PostAsJsonAsync("/api/offerings/join", new { join_code = joinCode });

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ma_sai_va_ma_dung_nhung_khong_co_ten_tra_ve_THONG_BAO_GIONG_HET_NHAU()
    {
        // Khác nhau một chữ là kẻ dò mã phân biệt được "mã sai" với "mã đúng nhưng không có tên",
        // tức dò ra mã hợp lệ chỉ bằng cách thử.
        var (_, joinCode, _, _) = await SeedLopCoDanhSachAsync();
        var nguoiLa = await DangNhapSinhVienAsync("nguoi-la-2@sv.edu.vn", "8888888888");

        static async Task<string> DetailAsync(HttpResponseMessage r) =>
            (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;

        var maDung = await nguoiLa.PostAsJsonAsync("/api/offerings/join", new { join_code = joinCode });
        var maBia = await nguoiLa.PostAsJsonAsync("/api/offerings/join", new { join_code = "ZZZZZZ" });

        maDung.StatusCode.ShouldBe(maBia.StatusCode);
        (await DetailAsync(maDung)).ShouldBe(await DetailAsync(maBia));
    }

    [Fact]
    public async Task Ma_lop_het_han_bi_tu_choi()
    {
        var (offeringId, joinCode, maSv, email) = await SeedLopCoDanhSachAsync();

        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var o = await db.CourseOfferings.SingleAsync(x => x.Id == offeringId);
            o.JoinCodeExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var sv = await DangNhapSinhVienAsync(email, maSv);
        var res = await sv.PostAsJsonAsync("/api/offerings/join", new { join_code = joinCode });

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sv_bat_buoc_doi_mat_khau_lan_dau_truoc_khi_dung_cac_api_khac()
    {
        var sv = await DangNhapSinhVienAsync("sv-phai-doi-mk@sv.edu.vn", "7777777777", mustChange: true);

        var res = await sv.GetAsync("/api/me/offerings");

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).ShouldContain("must_change_password");
    }

    [Fact]
    public async Task Doi_mat_khau_xong_thi_dung_duoc_cac_api_khac()
    {
        var sv = await DangNhapSinhVienAsync("sv-doi-mk-xong@sv.edu.vn", "6666666666", mustChange: true);

        var doi = await sv.PostAsJsonAsync("/api/auth/change-password",
            new { current_password = "Sinh@123", new_password = "MatKhauMoi@456" });
        doi.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Token cũ vẫn còn hiệu lực (access token sống ngắn), và cờ đã gỡ nên qua được middleware.
        (await sv.GetAsync("/api/me/offerings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doi_mat_khau_thu_hoi_toan_bo_refresh_token_cu()
    {
        // Đổi mật khẩu mà phiên cũ vẫn sống thì việc đổi gần như vô nghĩa.
        const string email = "sv-thu-hoi@sv.edu.vn";
        var sv = await DangNhapSinhVienAsync(email, "5555555555");

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "Sinh@123" });
        var refresh = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refresh_token").GetString()!;

        (await sv.PostAsJsonAsync("/api/auth/change-password",
            new { current_password = "Sinh@123", new_password = "MatKhauMoi@789" }))
            .EnsureSuccessStatusCode();

        var thu = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = refresh });

        thu.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Giang_vien_tao_tai_khoan_cho_ca_lop_va_sv_dang_nhap_duoc()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Enrollments.Add(new Enrollment
            {
                OfferingId = offeringId, StudentCode = $"22599{duoi}",
                FullName = "Sinh viên hàng loạt", Email = $"hangloat-{duoi}@sv.edu.vn",
            });
            await db.SaveChangesAsync();
        }

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/enrollments/create-accounts", null);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("created").GetInt32().ShouldBe(1);

        var tk = body.GetProperty("accounts").EnumerateArray().Single();
        var matKhau = tk.GetProperty("initial_password").GetString()!;
        var loginEmail = tk.GetProperty("login_email").GetString()!;

        // Đăng nhập được, nhưng bị chặn ngay cho tới khi đổi mật khẩu.
        var sv = factory.CreateClient();
        var dn = await sv.PostAsJsonAsync("/api/auth/login", new { email = loginEmail, password = matKhau });
        dn.StatusCode.ShouldBe(HttpStatusCode.OK);

        sv.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await dn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString());

        (await sv.GetAsync("/api/me/offerings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
