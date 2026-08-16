using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();

        // Gap #1: instructor_id is a real FK to the users table, not a free-floating Guid.
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Restrict);

        // An instructor must not have two subjects with the same code. A partial index, because
        // soft-deleted rows stay in the table and must not keep that code occupied.
        b.HasIndex(x => new { x.InstructorId, x.Code })
         .IsUnique()
         .HasFilter("NOT is_deleted");   // raw SQL: column names in snake_case

        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.SubjectId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class SubjectVersionConfiguration : IEntityTypeConfiguration<SubjectVersion>
{
    public void Configure(EntityTypeBuilder<SubjectVersion> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.VersionLabel).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // The SRS syllabus_json → typed jsonb, not a string.
        // A complex list NESTED INSIDE a ToJson() column has to be declared explicitly with
        // OwnsMany: EF cannot infer it and the model then fails to build ("Unable to determine
        // the relationship represented by navigation 'Syllabus.Outcomes'"). A List<string> needs
        // none of this, because that is a primitive collection.
        b.OwnsOne(x => x.Syllabus, s =>
        {
            s.ToJson();
            s.OwnsMany(y => y.Outcomes);
        });

        b.HasMany(x => x.Chapters).WithOne().HasForeignKey(c => c.SubjectVersionId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.SubjectId, x.Status });
        b.HasIndex(x => new { x.SubjectId, x.VersionLabel }).IsUnique();
    }
}

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.FoundationTags).HasColumnType("text[]");
        b.Property(x => x.UnlockRule).HasConversion<string>().HasMaxLength(32);

        b.HasMany(x => x.Lessons).WithOne().HasForeignKey(l => l.ChapterId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasIndex(x => new { x.SubjectVersionId, x.OrderIndex });
    }
}

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.FileUrl).HasMaxLength(1000);

        b.HasMany(x => x.Components).WithOne().HasForeignKey(c => c.LessonId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasIndex(x => new { x.ChapterId, x.OrderIndex });
    }
}

public sealed class LessonComponentConfiguration : IEntityTypeConfiguration<LessonComponent>
{
    public void Configure(EntityTypeBuilder<LessonComponent> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);

        // The SRS config_json → typed jsonb.
        // ComponentConfig contains NO enum today; the day one is added here it MUST get its own
        // .HasConversion<string>() — EF uses a dedicated JsonValueReaderWriter for a ToJson()
        // column and does NOT read the ASP.NET Core JsonStringEnumConverter.
        b.OwnsOne(x => x.Config, c =>
        {
            c.ToJson();
            c.OwnsMany(cfg => cfg.InlineQuestions);   // see the note in SubjectVersionConfiguration
        });

        // RefId deliberately declares NO FK: it points at either an Assignment OR a Quiz,
        // depending on Type. Consistency is enforced by SubjectVersionCloner and the validators.
        b.HasIndex(x => new { x.LessonId, x.OrderIndex });
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.SubmissionMode).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.MaxScore).HasPrecision(5, 2);
        b.Property(x => x.LatePenaltyPercentPerDay).HasPrecision(5, 2);

        // GroupConfig DOES contain the AllocationMode enum, so it needs its own
        // .HasConversion<string>(): EF uses JsonValueReaderWriter for a ToJson() column and does
        // NOT read the JsonStringEnumConverter configured at the HTTP layer. Without this line
        // the jsonb stores {"AllocationMode": 1} — no error, no failing test, just data that
        // changes meaning the day someone inserts a value into the middle of the enum.
        b.OwnsOne(x => x.GroupConfig, g =>
        {
            g.ToJson();
            g.Property(y => y.AllocationMode).HasConversion<string>();
        });

        b.HasMany(x => x.Hints).WithOne().HasForeignKey(h => h.AssignmentId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<SubjectVersion>().WithMany().HasForeignKey(x => x.SubjectVersionId)
         .OnDelete(DeleteBehavior.Cascade);

        // A rubric belongs to the instructor and is shared across many assignments, so deleting
        // an assignment must not touch the rubric.
        b.HasOne<Rubric>().WithMany().HasForeignKey(x => x.RubricId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.SubjectVersionId);
    }
}

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();

        b.HasOne<SubjectVersion>().WithMany().HasForeignKey(x => x.SubjectVersionId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        // Config contains NO enum today; the day one is added here it MUST get its own
        // .HasConversion<string>() — EF uses JsonValueReaderWriter for a ToJson() column and
        // does not read the HTTP layer's JsonStringEnumConverter.
        b.OwnsOne(x => x.Config, c => c.ToJson());

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.QuizId)
         .OnDelete(DeleteBehavior.Cascade);

        // Gap #3: exactly ONE of the two owners. Drop the CHECK and a quiz can belong to both a
        // subject version and a class offering, and cloning the version will duplicate it.
        b.ToTable(t => t.HasCheckConstraint("ck_quizzes_owner", """
            (subject_version_id IS NOT NULL AND offering_id IS NULL) OR
            (subject_version_id IS NULL     AND offering_id IS NOT NULL)
            """));

        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.SubjectVersionId);
        b.HasIndex(x => x.OfferingId);
    }
}
