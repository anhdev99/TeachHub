using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace TeachHub.Api.Tests;

/// <summary>
/// Dựng API thật trên một Postgres thật trong container. Không dùng InMemory provider:
/// hệ thống này phụ thuộc nặng vào jsonb, CHECK constraint và partial index — những thứ
/// InMemory không mô phỏng, nên test qua nó sẽ xanh giả.
/// </summary>
public class TeachHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Lớp con ghi đè để đổi cấu hình host (ví dụ tắt seed để test bootstrap).</summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraSettings => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _db.GetConnectionString(),
                ["Hangfire:Enabled"] = "false",
                ["Jwt:Key"] = "test-key-chi-dung-trong-test-va-du-dai-32-bytes!!",
                ["Jwt:Issuer"] = "teachhub-test",
                ["Jwt:Audience"] = "teachhub-test",
            });
            cfg.AddInMemoryCollection(ExtraSettings);
        });
    }

    public IServiceScope CreateServiceScope() => Services.CreateScope();

    public virtual async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Đọc khoá <c>access_token</c> bằng JsonElement chứ không map vào DTO: nếu ai đó bỏ
    /// AddJsonOptions thì API trả accessToken và test này đỏ ngay. Map vào DTO thì
    /// System.Text.Json khớp không phân biệt hoa thường và lỗi lọt qua.
    /// </summary>
    public async Task<HttpClient> LoginAsInstructorAsync()
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "instructor@teachhub.local", password = "Teach@123" });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Đăng nhập bằng một tài khoản bất kỳ; tạo mới nếu chưa có (dùng cho test nhiều giảng viên).</summary>
    public async Task<HttpClient> LoginAsAsync(string email, string password = "Teach@123")
    {
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TeachHub.Domain.Identity.AppUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var u = new TeachHub.Domain.Identity.AppUser
                {
                    UserName = email, Email = email, FullName = email, EmailConfirmed = true,
                };
                await users.CreateAsync(u, password);
                await users.AddToRoleAsync(u, TeachHub.Domain.Identity.Roles.Instructor);
            }
        }

        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Tạo một môn học kèm phiên bản draft đầu tiên; trả về (subjectId, versionId).</summary>
    public static async Task<(Guid SubjectId, Guid VersionId)> SeedSubjectWithDraftAsync(HttpClient gv)
    {
        var ma = $"IN{Guid.CreateVersion7().ToString("N")[^6..].ToUpperInvariant()}";
        var res = await gv.PostAsJsonAsync("/api/subjects", new { code = ma, name = "Môn kiểm thử" });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();

        var subjectId = json.GetProperty("id").GetGuid();
        var versions = await gv.GetFromJsonAsync<JsonElement>($"/api/subjects/{subjectId}/versions");
        return (subjectId, versions.EnumerateArray().First().GetProperty("id").GetGuid());
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}
