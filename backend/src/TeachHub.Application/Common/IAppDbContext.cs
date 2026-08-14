using Microsoft.EntityFrameworkCore;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Common;

/// <summary>
/// Cổng truy cập dữ liệu. Handler viết LINQ trực tiếp trên các DbSet này (ADR-002:
/// không dùng Repository generic). Mỗi phase sau sẽ bổ sung thêm DbSet vào đây.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
