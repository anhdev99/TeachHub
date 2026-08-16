using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Assessments;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Groups;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.GroupId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.OfferingId, x.Name }).IsUnique();
    }
}

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> b)
    {
        b.HasKey(x => x.Id);

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);

        // A student cannot be in two groups at once within the same class offering. An Enrollment
        // is already pinned to one offering, so a unique index on enrollment_id is enough — no
        // need to repeat offering_id. Let the database hold this invariant instead of the handler
        // remembering it: two concurrent "add to group" requests will not see each other.
        b.HasIndex(x => x.EnrollmentId).IsUnique();
    }
}

public sealed class GradeAuditLogConfiguration : IEntityTypeConfiguration<GradeAuditLog>
{
    public void Configure(EntityTypeBuilder<GradeAuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.OldValue).HasPrecision(5, 2);
        b.Property(x => x.NewValue).HasPrecision(5, 2);
        b.Property(x => x.Reason).HasMaxLength(500);

        // Restrict on every side: the grade audit log is NEVER cascade-deleted, not even when the
        // class offering is deleted. It exists precisely for the times when the original data is
        // gone.
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ChangedBy)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TargetType, x.TargetId, x.ChangedAt });
        b.HasIndex(x => new { x.OfferingId, x.ChangedAt });
    }
}

public sealed class FinalAssessmentConfiguration : IEntityTypeConfiguration<FinalAssessment>
{
    public void Configure(EntityTypeBuilder<FinalAssessment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.SubmissionMode).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.MaxScore).HasPrecision(5, 2);

        // GroupConfig contains the AllocationMode enum → it needs its own .HasConversion<string>().
        // EF uses JsonValueReaderWriter for a ToJson() column and does NOT read the
        // JsonStringEnumConverter configured at the HTTP layer; without this line the jsonb stores
        // numbers, and the data changes meaning the day someone inserts a value into the middle
        // of the enum.
        b.OwnsOne(x => x.GroupConfig, g =>
        {
            g.ToJson();
            g.Property(y => y.AllocationMode).HasConversion<string>();
        });

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Rubric>().WithMany().HasForeignKey(x => x.RubricId)
         .OnDelete(DeleteBehavior.Restrict);

        // grade_column_id has no FK yet: the grade_columns table belongs to Phase 4b, and the
        // constraint will be added there.
        b.HasMany(x => x.Slots).WithOne().HasForeignKey(s => s.FinalAssessmentId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OfferingId);
    }
}
