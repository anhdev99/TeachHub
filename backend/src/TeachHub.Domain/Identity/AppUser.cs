using Microsoft.AspNetCore.Identity;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Identity;

public class AppUser : IdentityUser<Guid>, IAuditable
{
    public AppUser() => Id = Guid.CreateVersion7();

    public required string FullName { get; set; }

    /// <summary>Student number — present only for students, null for instructors/teaching assistants.</summary>
    public string? StudentCode { get; set; }

    /// <summary>Students imported from Excel must change their password on first login (SRS section 8).</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();
    public AppRole(string name) : this() => Name = name;
}
