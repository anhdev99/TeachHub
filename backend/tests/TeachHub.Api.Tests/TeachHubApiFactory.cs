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
public sealed class TeachHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _db.GetConnectionString(),
                ["Hangfire:Enabled"] = "false",
                ["Jwt:Key"] = "test-key-chi-dung-trong-test-va-du-dai-32-bytes!!",
                ["Jwt:Issuer"] = "teachhub-test",
                ["Jwt:Audience"] = "teachhub-test",
            }));
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}
