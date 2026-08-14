using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Domain.Identity;

namespace TeachHub.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<AppRole>>();
        foreach (var r in Roles.All)
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new AppRole(r));

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        const string email = "instructor@teachhub.local";
        if (await users.FindByEmailAsync(email) is null)
        {
            var user = new AppUser
            {
                UserName = email, Email = email,
                FullName = "Giảng viên", EmailConfirmed = true,
            };
            await users.CreateAsync(user, "Teach@123");
            await users.AddToRoleAsync(user, Roles.Instructor);
        }
    }
}
