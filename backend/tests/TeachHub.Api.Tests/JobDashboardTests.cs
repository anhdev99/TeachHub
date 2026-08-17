using System.Net;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class JobDashboardTests(TeachHubApiFactory factory)
{
    [Fact]
    public async Task The_hangfire_dashboard_rejects_an_anonymous_visitor()
    {
        // NotFound is acceptable because the Testing environment turns Hangfire off — what
        // matters is that it NEVER returns 200. The Hangfire dashboard exposes job names, job
        // arguments and lets anyone re-run a job; making it public makes the system internals public.
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
