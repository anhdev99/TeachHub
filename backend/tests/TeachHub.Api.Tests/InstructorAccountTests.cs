using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Identity;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

/// <summary>Host không chạy seed — để test được bootstrap trên hệ thống thật sự trống.</summary>
public sealed class NoSeedApiFactory : TeachHubApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraSettings =>
        [new("Seed:Enabled", "false")];
}

[Collection(NoSeedCollection.Ten)]
public class BootstrapTests(NoSeedApiFactory factory)
{
    [Fact]
    public async Task Bootstrap_tao_duoc_giang_vien_dau_tien_khi_he_thong_trong()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/bootstrap",
            new { email = "dau-tien@teachhub.local", full_name = "Người đầu tiên", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Đăng nhập được ngay bằng tài khoản vừa tạo, và có vai trò Instructor.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "dau-tien@teachhub.local", password = "Teach@123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Bootstrap_lan_hai_luon_tra_409()
    {
        // Kể cả khi giảng viên đầu tiên đã bị vô hiệu hoá. Nếu không, ai biết đường dẫn này
        // cũng tự tạo được tài khoản giảng viên — đúng thứ mà việc bỏ đăng ký công khai muốn tránh.
        await factory.CreateClient().PostAsJsonAsync("/api/auth/bootstrap",
            new { email = "mot@teachhub.local", full_name = "Một", password = "Teach@123" });

        var lanHai = await factory.CreateClient().PostAsJsonAsync("/api/auth/bootstrap",
            new { email = "hai@teachhub.local", full_name = "Hai", password = "Teach@123" });

        lanHai.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

[Collection(TeachHubCollection.Ten)]
public class InstructorAccountTests(TeachHubApiFactory factory)
{
    private static object MoiBody(string email) => new { email };

    private async Task<JsonElement> MoiAsync(HttpClient gv, string email)
    {
        var res = await gv.PostAsJsonAsync("/api/instructor/invites", MoiBody(email));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Moi_bang_tai_khoan_giang_vien_tra_ve_token_dung_MOT_lan()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var invite = await MoiAsync(gv, "moi-1@teachhub.local");

        invite.GetProperty("token").GetString().ShouldNotBeNullOrWhiteSpace();

        // Danh sách lời mời KHÔNG được trả lại token — nó chỉ hiện đúng một lần lúc tạo.
        var ds = await gv.GetFromJsonAsync<JsonElement>("/api/instructor/invites");
        foreach (var x in ds.EnumerateArray())
            x.TryGetProperty("token", out _)
             .ShouldBeFalse("Danh sách lời mời không được trả lại token — nó chỉ hiện một lần lúc tạo.");
    }

    [Fact]
    public async Task Moi_khi_chua_dang_nhap_tra_401()
    {
        var res = await factory.CreateClient()
            .PostAsJsonAsync("/api/instructor/invites", MoiBody("ai-do@teachhub.local"));

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dang_ky_bang_token_hop_le_tao_duoc_tai_khoan_va_danh_dau_token_da_dung()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var token = (await MoiAsync(gv, "moi-2@teachhub.local")).GetProperty("token").GetString()!;

        var kiemTra = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/auth/invites/{token}");
        kiemTra.GetProperty("usable").GetBoolean().ShouldBeTrue();

        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token, full_name = "Đồng nghiệp", password = "Teach@123" });
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var sau = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/auth/invites/{token}");
        sau.GetProperty("usable").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Dung_lai_token_da_dung_tra_400()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var token = (await MoiAsync(gv, "moi-3@teachhub.local")).GetProperty("token").GetString()!;

        await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token, full_name = "Lần một", password = "Teach@123" });

        var lanHai = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token, full_name = "Lần hai", password = "Teach@123" });

        lanHai.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_het_han_tra_400()
    {
        // Gieo thẳng một lời mời đã quá hạn vào DB thay vì chờ 7 ngày.
        const string tokenThat = "token-da-het-han-dung-cho-test";
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nguoiMoi = await db.Set<AppUser>().FirstAsync();
            db.Set<InstructorInvite>().Add(new InstructorInvite
            {
                Email = "het-han@teachhub.local",
                TokenHash = InstructorInvite.HashToken(tokenThat),
                InvitedBy = nguoiMoi.Id,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3),
            });
            await db.SaveChangesAsync();
        }

        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token = tokenThat, full_name = "Muộn quá", password = "Teach@123" });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_sai_va_token_het_han_tra_ve_THONG_BAO_GIONG_HET_NHAU()
    {
        // Khác nhau một chữ là kẻ tấn công phân biệt được "token này từng tồn tại" với
        // "token bịa", tức dò được không gian token bằng cách thử.
        const string tokenHetHan = "token-het-han-de-so-sanh-thong-bao";
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nguoiMoi = await db.Set<AppUser>().FirstAsync();
            db.Set<InstructorInvite>().Add(new InstructorInvite
            {
                Email = "so-sanh@teachhub.local",
                TokenHash = InstructorInvite.HashToken(tokenHetHan),
                InvitedBy = nguoiMoi.Id,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3),
            });
            await db.SaveChangesAsync();
        }

        static async Task<string> ThanAsync(HttpResponseMessage r)
        {
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return j.GetProperty("detail").GetString()!;
        }

        var heHan = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token = tokenHetHan, full_name = "A", password = "Teach@123" });
        var bia = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token = "token-hoan-toan-bia-dat", full_name = "B", password = "Teach@123" });

        heHan.StatusCode.ShouldBe(bia.StatusCode);
        (await ThanAsync(heHan)).ShouldBe(await ThanAsync(bia));
    }

    [Fact]
    public async Task Moi_email_da_co_tai_khoan_tra_409()
    {
        // Ở đây nói thẳng lý do được: người mời là giảng viên đã đăng nhập, đã được tin tưởng.
        // Khác hẳn luồng quên-mật-khẩu, nơi phải mơ hồ để không lộ email nào tồn tại.
        var gv = await factory.LoginAsInstructorAsync();

        var res = await gv.PostAsJsonAsync("/api/instructor/invites",
            MoiBody("instructor@teachhub.local"));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Giang_vien_vua_duoc_tao_KHONG_thay_du_lieu_cua_nguoi_moi_minh()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var token = (await MoiAsync(gv, "cach-ly@teachhub.local")).GetProperty("token").GetString()!;

        await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token, full_name = "Người mới", password = "Teach@123" });

        var moi = factory.CreateClient();
        var login = await moi.PostAsJsonAsync("/api/auth/login",
            new { email = "cach-ly@teachhub.local", password = "Teach@123" });
        var access = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString();
        moi.DefaultRequestHeaders.Authorization = new("Bearer", access);

        var ds = await moi.GetFromJsonAsync<JsonElement>("/api/instructor/invites");

        ds.GetArrayLength().ShouldBe(0,
            "Giảng viên mới phải thấy danh sách rỗng — lời mời của người mời mình là dữ liệu của người khác.");
    }
}
