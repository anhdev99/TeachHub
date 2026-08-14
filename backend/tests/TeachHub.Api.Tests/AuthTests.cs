using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

public class AuthTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task Dang_nhap_dung_thi_tra_ve_access_va_refresh_token()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Đọc bằng JsonElement với khoá snake_case tường minh: map vào DTO thì
        // System.Text.Json khớp không phân biệt hoa thường và bỏ AddJsonOptions vẫn xanh.
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("access_token").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("refresh_token").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("expires_in").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Sai_mat_khau_tra_ve_401_dang_problem_details_va_khong_lo_email_co_ton_tai_hay_khong()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "sai-mat-khau" });

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var text = await res.Content.ReadAsStringAsync();
        // Thông báo phải chung chung — không được tiết lộ email nào tồn tại
        text.ShouldNotContain("không tìm thấy", Case.Insensitive);
    }

    [Fact]
    public async Task Email_sai_dinh_dang_tra_ve_400_kem_danh_sach_loi_theo_truong()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "khong-phai-email", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"errors\"");
    }

    [Fact]
    public async Task Endpoint_can_xac_thuc_tu_choi_request_khong_co_token()
    {
        var res = await Client.GetAsync("/api/auth/me");
        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_can_xac_thuc_tra_ve_ho_so_khi_co_token()
    {
        var client = await factory.LoginAsInstructorAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        me.GetProperty("email").GetString().ShouldBe("instructor@teachhub.local");
        me.GetProperty("full_name").GetString().ShouldBe("Giảng viên");
        me.GetProperty("roles").EnumerateArray()
          .Select(r => r.GetString()).ShouldContain("Instructor");
    }
}
