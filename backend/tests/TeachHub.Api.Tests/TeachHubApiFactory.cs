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

    public async Task InitializeAsync()
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

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}
