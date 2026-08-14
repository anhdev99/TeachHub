using System.Net;
using Shouldly;

namespace TeachHub.Api.Tests;

public class HostSmokeTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    [Fact]
    public async Task Health_tra_ve_200_khi_db_song()
    {
        var res = await factory.CreateClient().GetAsync("/health");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_doc_sinh_duoc_va_la_ban_3_1()
    {
        var doc = await factory.CreateClient().GetStringAsync("/openapi/v1.json");
        doc.ShouldContain("\"openapi\": \"3.1");
    }

    [Fact]
    public async Task OpenApi_khai_bao_snake_case_giong_het_phan_hoi_that()
    {
        // Runtime dùng AddJsonOptions, còn OpenAPI dùng ConfigureHttpJsonOptions — hai IOptions
        // khác nhau. Thiếu cái thứ hai thì API trả access_token nhưng tài liệu khai accessToken,
        // và client TypeScript sinh từ tài liệu đó sẽ gõ sai khóa mà vẫn biên dịch được.
        var doc = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        doc.ShouldContain("access_token");
        doc.ShouldNotContain("accessToken");
    }

    [Fact]
    public async Task Loi_tra_ve_dung_dinh_dang_problem_details()
    {
        // Phải gọi bằng client ĐÃ đăng nhập: fallback policy bắt buộc xác thực cho cả request
        // không khớp endpoint nào, nên client ẩn danh sẽ nhận 401 chứ không phải 404.
        // Test này muốn kiểm định dạng problem+json của 404, nên phải vượt tầng xác thực trước.
        var client = await factory.LoginAsInstructorAsync();

        var res = await client.GetAsync("/api/khong-ton-tai");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
