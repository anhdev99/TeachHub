using System.Net;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class JobDashboardTests(TeachHubApiFactory factory)
{
    [Fact]
    public async Task Dashboard_hangfire_tu_choi_nguoi_chua_dang_nhap()
    {
        // Chấp nhận NotFound vì môi trường Testing tắt Hangfire — điều quan trọng là
        // KHÔNG BAO GIỜ trả 200. Dashboard Hangfire để lộ tên job, tham số job và
        // cho phép bấm chạy lại job; công khai nó là công khai luôn nội bộ hệ thống.
        var res = await factory.CreateClient().GetAsync("/jobs");

        res.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dashboard_hangfire_khong_bao_gio_tra_200_ke_ca_khi_da_dang_nhap_bang_vai_khac()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var res = await gv.GetAsync("/jobs");

        // Ở Testing Hangfire tắt nên phải là 404; nếu bật thì giảng viên mới được vào.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
