using Microsoft.AspNetCore.Identity;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

public class AppUser : IdentityUser<Guid>, IAuditable
{
    public AppUser() => Id = Guid.CreateVersion7();

    public required string FullName { get; set; }

    /// <summary>MSSV — chỉ có với sinh viên, null với giảng viên/trợ giảng.</summary>
    public string? StudentCode { get; set; }

    /// <summary>SV import từ Excel phải đổi mật khẩu ở lần đăng nhập đầu (SRS mục 8).</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();
    public AppRole(string name) : this() => Name = name;
}
