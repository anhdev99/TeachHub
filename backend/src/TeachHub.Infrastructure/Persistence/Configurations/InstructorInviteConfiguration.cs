using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Identity;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class InstructorInviteConfiguration : IEntityTypeConfiguration<InstructorInvite>
{
    public void Configure(EntityTypeBuilder<InstructorInvite> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.InvitedBy, x.AcceptedAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InvitedBy).OnDelete(DeleteBehavior.Cascade);
    }
}
