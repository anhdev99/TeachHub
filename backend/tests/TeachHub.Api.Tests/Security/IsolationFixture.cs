using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Domain.Identity;

namespace TeachHub.Api.Tests.Security;

/// <summary>
/// Dựng "thế giới của giảng viên A" rồi giao cho ma trận ba client: chính A, một giảng viên B
/// hoàn toàn độc lập, và một tài khoản chỉ có vai trò trợ giảng.
///
/// Phase nào thêm một loại tài nguyên mới thuộc về giảng viên thì seed thêm ở đây VÀ thêm dòng
/// vào ma trận — đó là điều kiện của Definition of Done mục 7, không phải việc tuỳ tâm.
/// Ở Phase 0 mới chỉ có đúng một loại: InstructorInvite.
/// </summary>
public sealed class IsolationFixture : TeachHubApiFactory
{
    public HttpClient GvA { get; private set; } = null!;
    public HttpClient GvB { get; private set; } = null!;
    public HttpClient TroGiang { get; private set; } = null!;

    /// <summary>Id các tài nguyên THUỘC VỀ giảng viên A, để ma trận thay vào template.</summary>
    private readonly Dictionary<string, string> _idCuaA = [];

    private const string MatKhau = "Teach@123";
    private const string EmailB  = "gv-b@teachhub.local";
    private const string EmailTa = "tro-giang@teachhub.local";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        GvA = await LoginAsInstructorAsync();

        // ---- Thế giới của A: một lời mời do A tạo ----
        var invite = await GvA.PostAsJsonAsync("/api/instructor/invites",
            new { email = "nguoi-duoc-a-moi@teachhub.local" });
        invite.EnsureSuccessStatusCode();
        var inviteJson = await invite.Content.ReadFromJsonAsync<JsonElement>();
        _idCuaA["inviteId"] = inviteJson.GetProperty("id").GetString()!;

        // ---- Thế giới nội dung của A: môn → phiên bản → chương → bài → component ----
        var (subjectId, versionId) = await SeedSubjectWithDraftAsync(GvA);
        _idCuaA["subjectId"] = subjectId.ToString();
        _idCuaA["versionId"] = versionId.ToString();

        var chuong = await GvA.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương của A" });
        chuong.EnsureSuccessStatusCode();
        var chapterId = (await chuong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        _idCuaA["chapterId"] = chapterId.ToString();

        var bai = await GvA.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons",
            new { title = "Bài của A" });
        bai.EnsureSuccessStatusCode();
        var lessonId = (await bai.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        _idCuaA["lessonId"] = lessonId.ToString();

        var cp = await GvA.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new { type = "hook" });
        cp.EnsureSuccessStatusCode();
        _idCuaA["componentId"] =
            (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // ---- Giảng viên B: độc lập hoàn toàn, được tạo qua đúng luồng lời mời ----
        var moiB = await GvA.PostAsJsonAsync("/api/instructor/invites", new { email = EmailB });
        var tokenB = (await moiB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        (await CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token = tokenB, full_name = "Giảng viên B", password = MatKhau }))
            .EnsureSuccessStatusCode();
        GvB = await DangNhapAsync(EmailB);

        // ---- Trợ giảng: vai trò tồn tại nhưng KHÔNG có policy nào cấp quyền (ADR-013) ----
        using (var scope = CreateServiceScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            if (!await roles.RoleExistsAsync(Roles.TeachingAssistant))
                await roles.CreateAsync(new AppRole(Roles.TeachingAssistant));

            var ta = new AppUser
            {
                UserName = EmailTa, Email = EmailTa,
                FullName = "Trợ giảng", EmailConfirmed = true,
            };
            await users.CreateAsync(ta, MatKhau);
            await users.AddToRoleAsync(ta, Roles.TeachingAssistant);
        }
        TroGiang = await DangNhapAsync(EmailTa);
    }

    private async Task<HttpClient> DangNhapAsync(string email)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = MatKhau });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Thay <c>{inviteId}</c>… trong template bằng id thật của giảng viên A.</summary>
    public string Resolve(string template)
    {
        var url = template;
        foreach (var (khoa, giaTri) in _idCuaA)
            url = url.Replace($"{{{khoa}}}", giaTri, StringComparison.Ordinal);

        if (url.Contains('{', StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Template '{template}' còn placeholder chưa được seed trong IsolationFixture: {url}");

        return url;
    }
}
