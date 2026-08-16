using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Assessments;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Content;
using TeachHub.Domain.Groups;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Submissions;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();

        // Gap #1: instructor_id is a real FK, not a free-floating Guid.
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Criteria).WithOne().HasForeignKey(c => c.RubricId)
         .OnDelete(DeleteBehavior.Cascade);

        // MaxPoints is computed from Criteria — it has no backing column.
        b.Ignore(x => x.MaxPoints);

        b.HasIndex(x => new { x.InstructorId, x.IsTemplate });
    }
}

public sealed class RubricCriterionConfiguration : IEntityTypeConfiguration<RubricCriterion>
{
    public void Configure(EntityTypeBuilder<RubricCriterion> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.MaxPoints).HasPrecision(5, 2);

        // The level descriptions → jsonb. CriterionLevel contains no enum today; the day one is
        // added here it MUST get its own .HasConversion<string>() — EF uses JsonValueReaderWriter
        // for a ToJson() column and does NOT read JsonStringEnumConverter.
        b.OwnsMany(x => x.Levels, l =>
        {
            l.ToJson();
            l.Property(y => y.Points).HasPrecision(5, 2);
        });

        b.HasIndex(x => new { x.RubricId, x.OrderIndex });
    }
}

public sealed class AssignmentHintConfiguration : IEntityTypeConfiguration<AssignmentHint>
{
    public void Configure(EntityTypeBuilder<AssignmentHint> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.PenaltyPercent).HasPrecision(5, 2);

        b.HasIndex(x => new { x.AssignmentId, x.OrderIndex });
    }
}

public sealed class SubmissionSlotConfiguration : IEntityTypeConfiguration<SubmissionSlot>
{
    public void Configure(EntityTypeBuilder<SubmissionSlot> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SlotKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Label).HasMaxLength(200).IsRequired();
        b.Property(x => x.AcceptedFileTypes).HasColumnType("text[]");

        b.HasOne<Assignment>().WithMany(a => a.Slots)
         .HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);

        // final_assessment_id has no target table yet: FinalAssessment belongs to Phase 4a. The
        // column and the CHECK constraint are put in place now so Phase 4a only has to add one
        // single FK, instead of re-migrating a table that already holds real submissions.
        b.OwnsOne(x => x.RequiredStructure, r => r.ToJson());

        b.Ignore(x => x.AcceptsZip);
        b.Ignore(x => x.MaxFileSizeBytes);

        // The values inside the CHECK must match the C# enum names EXACTLY, because the column
        // stores strings via HasConversion<string>(). Writing 'assignment' in snake_case here
        // still lets the migration apply cleanly, but then EVERY INSERT is rejected — and that
        // only surfaces when the first slot is created.
        b.ToTable(t => t.HasCheckConstraint("ck_submission_slots_owner", """
            (owner_type = 'Assignment'      AND assignment_id IS NOT NULL AND final_assessment_id IS NULL) OR
            (owner_type = 'FinalAssessment' AND final_assessment_id IS NOT NULL AND assignment_id IS NULL)
            """));

        b.HasIndex(x => new { x.AssignmentId, x.OrderIndex });
    }
}

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SubmittedByType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Score).HasPrecision(5, 2);

        b.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId)
         .OnDelete(DeleteBehavior.Restrict);   // NO cascade: a submission is grading evidence
        b.HasOne<FinalAssessment>().WithMany().HasForeignKey(x => x.FinalAssessmentId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudentGroup>().WithMany().HasForeignKey(x => x.GroupId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Files).WithOne().HasForeignKey(f => f.SubmissionId)
         .OnDelete(DeleteBehavior.Cascade);

        // rubric_scores_json: a Dictionary is not one of the primitive collections EF maps out of
        // the box, so it goes through an explicit converter. The ValueComparer is MANDATORY —
        // without it EF compares the Dictionary by reference, never notices a change to its
        // contents, and an edited rubric score is simply not written to the DB, with no error.
        b.Property(x => x.RubricScores)
         .HasColumnType("jsonb")
         .HasConversion(
             v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
             v => JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(v, (JsonSerializerOptions?)null)
                  ?? new Dictionary<Guid, decimal>(),
             new ValueComparer<Dictionary<Guid, decimal>>(
                 (a, c) => a != null && c != null && a.Count == c.Count && !a.Except(c).Any(),
                 v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value)),
                 v => new Dictionary<Guid, decimal>(v)));

        b.ToTable(t => t.HasCheckConstraint("ck_submissions_owner", """
            (owner_type = 'Assignment'      AND assignment_id IS NOT NULL AND final_assessment_id IS NULL) OR
            (owner_type = 'FinalAssessment' AND final_assessment_id IS NOT NULL AND assignment_id IS NULL)
            """));

        // The invariant "each submitter has exactly ONE current version" is enforced by the
        // database, not by the handler remembering to. Without this index a double-click on the
        // "submit for grading" button creates two is_current rows, and the gradebook's Single()
        // throws in the middle of a grading session.
        // HasFilter takes RAW SQL, so column names are written in snake_case.
        b.HasIndex(x => new { x.AssignmentId, x.FinalAssessmentId, x.EnrollmentId, x.GroupId })
         .IsUnique()
         .HasFilter("is_current")
         .HasDatabaseName("ux_submission_current");

        b.HasIndex(x => new { x.OfferingId, x.Status });
    }
}

public sealed class SubmissionFileConfiguration : IEntityTypeConfiguration<SubmissionFile>
{
    public void Configure(EntityTypeBuilder<SubmissionFile> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SlotKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.FilePath).HasMaxLength(1000);
        b.Property(x => x.ThumbnailPath).HasMaxLength(1000);
        b.Property(x => x.ExternalUrl).HasMaxLength(1000);
        b.Property(x => x.ContentType).HasMaxLength(200);
        b.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.MissingStructureItems).HasColumnType("text[]");

        b.HasOne(x => x.Slot).WithMany()
         .HasForeignKey(x => x.SlotId).OnDelete(DeleteBehavior.Restrict);

        // file_tree_json → typed jsonb. A complex list NESTED inside a ToJson() column has to be
        // declared explicitly with OwnsMany; EF cannot infer it.
        b.OwnsOne(x => x.FileTree, t =>
        {
            t.ToJson();
            t.OwnsMany(y => y.Files);
        });

        b.Ignore(x => x.IsLink);

        b.HasIndex(x => new { x.SubmissionId, x.SlotKey });
    }
}
