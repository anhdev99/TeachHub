using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Common;
using TeachHub.Domain.Identity;

namespace TeachHub.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    DbSet<AppUser> IAppDbContext.Users => Users;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ASP.NET Core Identity đặt tên 7 bảng của nó TƯỜNG MINH bằng ToTable("AspNetUsers"),
        // và cấu hình tường minh luôn thắng convention. Vì thế UseSnakeCaseNamingConvention()
        // đổi được tên cột, khoá chính và khoá ngoại, nhưng KHÔNG đổi được tên bảng — schema
        // sẽ lẫn lộn "AspNetUsers" với cột "normalized_email". Đổi lại đúng 7 bảng đó ở đây
        // để toàn bộ schema thống nhất snake_case (quy ước §5 của tài liệu kiến trúc).
        b.Entity<AppUser>().ToTable("asp_net_users");
        b.Entity<AppRole>().ToTable("asp_net_roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");
        b.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");

        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        foreach (var e in ChangeTracker.Entries<IAuditable>())
        {
            if (e.State == EntityState.Added)    e.Entity.CreatedAt = now;
            if (e.State == EntityState.Modified) e.Entity.UpdatedAt = now;
        }
        foreach (var e in ChangeTracker.Entries<ISoftDeletable>().Where(x => x.State == EntityState.Deleted))
        {
            e.State = EntityState.Modified;      // xóa mềm — không bao giờ DELETE nội dung dạy học
            e.Entity.IsDeleted = true;
            e.Entity.DeletedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }
}
