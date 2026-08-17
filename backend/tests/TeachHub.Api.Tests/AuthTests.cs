using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class AuthTests(TeachHubApiFactory factory)
{
    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task Logging_in_with_the_right_credentials_returns_an_access_token_and_a_refresh_token()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Read through JsonElement with explicit snake_case keys: mapping into a DTO lets
        // System.Text.Json match case-insensitively, so this stays green even with AddJsonOptions removed.
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("access_token").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("refresh_token").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("expires_in").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_wrong_password_returns_401_as_problem_details_and_never_leaks_whether_the_email_exists()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "sai-mat-khau" });

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var text = await res.Content.ReadAsStringAsync();
        // The message must stay generic — it must not reveal which emails exist
        text.ShouldNotContain("không tìm thấy", Case.Insensitive);
    }

    [Fact]
    public async Task Email_sai_dinh_dang_tra_ve_400_kem_danh_sach_loi_theo_truong()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "khong-phai-email", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var loiEmail = body.GetProperty("errors").GetProperty("Email")
            .EnumerateArray().Select(x => x.GetString()).ToArray();

        // Thông báo lỗi trả cho người dùng phải là tiếng Việt (quy ước dự án). Mặc định của
        // FluentValidation là tiếng Anh, nên thiếu .WithMessage() là sinh viên nhận được
        // "'Email' is not a valid email address." ngay trên màn hình nộp bài.
        loiEmail.ShouldContain("Email không đúng định dạng.");
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
