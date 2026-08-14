using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class RefreshTokenTests(TeachHubApiFactory factory)
{
    private async Task<(string access, string refresh)> LoginAsync()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "Teach@123" });
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (j.GetProperty("access_token").GetString()!, j.GetProperty("refresh_token").GetString()!);
    }

    [Fact]
    public async Task Refresh_hop_le_tra_ve_cap_token_moi_khac_cap_cu()
    {
        var (_, refresh) = await LoginAsync();
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = refresh });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        j.GetProperty("refresh_token").GetString().ShouldNotBe(refresh);
    }

    [Fact]
    public async Task Dung_lai_refresh_token_cu_bi_tu_choi()
    {
        var (_, refresh) = await LoginAsync();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refresh });

        var lanHai = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = refresh });

        lanHai.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dung_lai_token_cu_thu_hoi_luon_ca_token_moi_nhat()
    {
        // Kịch bản: token bị đánh cắp. Kẻ tấn công dùng token cũ ⇒ hệ thống phải
        // coi cả phiên đó là đã lộ và buộc đăng nhập lại, không chỉ từ chối một request.
        var (_, refresh1) = await LoginAsync();
        var r2 = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refresh1 });
        var refresh2 = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refresh_token").GetString()!;

        await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refresh1 }); // tái sử dụng

        var r3 = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refresh2 });
        r3.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_bia_dat_bi_tu_choi()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = "token-bia-hoan-toan" });

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
