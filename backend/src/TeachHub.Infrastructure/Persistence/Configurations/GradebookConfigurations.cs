using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class GradeColumnConfiguration : IEntityTypeConfiguration<GradeColumn>
{
    public void Configure(EntityTypeBuilder<GradeColumn> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.WeightPercent).HasPrecision(5, 2);

        // Aggregation DOES contain the Kind enum → it needs its own .HasConversion<string>().
        // EF uses JsonValueReaderWriter for a ToJson() column and does NOT read the HTTP layer's
        // JsonStringEnumConverter. Without this line the jsonb stores {"Kind": 6} — no error, no
        // failing test, but every later JSON query by enum name silently misses, and the day
        // someone inserts a value into the middle of the enum, all the old data changes meaning.
        b.OwnsOne(x => x.Aggregation, a =>
        {
            a.ToJson();
            a.Property(y => y.Kind).HasConversion<string>();
        });

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Sources).WithOne().HasForeignKey(s => s.GradeColumnId)
         .OnDelete(DeleteBehavior.Cascade);

        // The constraint MUST live in the database, not only in a validator: columns are also
        // created by copying a template and by the "apply a different template" action — neither
        // of which goes through the configuration screen.
        b.HasIndex(x => new { x.OfferingId, x.Key }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint("ck_grade_column_key_slug",
            "key ~ '^[a-z0-9_]+$'"));
    }
}

public sealed class GradeColumnSourceConfiguration : IEntityTypeConfiguration<GradeColumnSource>
{
    public void Configure(EntityTypeBuilder<GradeColumnSource> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.WeightWithinColumn).HasPrecision(5, 2);

        // source_id points at an Assignment OR a Quiz OR a FinalAssessment depending on
        // SourceType, so it deliberately declares NO FK — consistent with Phase 1's
        // LessonComponent.RefId.
        b.HasIndex(x => new { x.GradeColumnId, x.SourceType, x.SourceId }).IsUnique();
    }
}

public sealed class GradeRecordConfiguration : IEntityTypeConfiguration<GradeRecord>
{
    public void Configure(EntityTypeBuilder<GradeRecord> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<GradeColumn>().WithMany().HasForeignKey(x => x.GradeColumnId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EnrollmentId, x.GradeColumnId }).IsUnique();
    }
}

public sealed class GradeSchemeTemplateConfiguration : IEntityTypeConfiguration<GradeSchemeTemplate>
{
    public void Configure(EntityTypeBuilder<GradeSchemeTemplate> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TemplateId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.InstructorId, x.Name }).IsUnique();
    }
}

public sealed class GradeSchemeTemplateItemConfiguration : IEntityTypeConfiguration<GradeSchemeTemplateItem>
{
    public void Configure(EntityTypeBuilder<GradeSchemeTemplateItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.WeightPercent).HasPrecision(5, 2);

        // EVERY place that has a GradeAggregation-typed property needs ITS OWN OwnsOne block —
        // EF cannot infer it from another entity's configuration. Without this line the EF model
        // fails to build and the application will not start, with an error message that does not
        // point at what is missing.
        b.OwnsOne(x => x.Aggregation, a =>
        {
            a.ToJson();
            a.Property(y => y.Kind).HasConversion<string>();
        });

        b.HasIndex(x => new { x.TemplateId, x.Key }).IsUnique();
    }
}

public sealed class BonusPointReasonTemplateConfiguration : IEntityTypeConfiguration<BonusPointReasonTemplate>
{
    public void Configure(EntityTypeBuilder<BonusPointReasonTemplate> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).HasMaxLength(200).IsRequired();
        b.Property(x => x.DefaultPoints).HasPrecision(5, 2);
        b.Property(x => x.DefaultTarget).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DefaultColumnKey).HasMaxLength(64);

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.InstructorId, x.OrderIndex });
    }
}

public sealed class BonusPointConfiguration : IEntityTypeConfiguration<BonusPoint>
{
    public void Configure(EntityTypeBuilder<BonusPoint> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Target).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ContextType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Points).HasPrecision(5, 2);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<GradeColumn>().WithMany().HasForeignKey(x => x.TargetColumnId)
         .OnDelete(DeleteBehavior.SetNull);
        b.HasOne<BonusPointReasonTemplate>().WithMany().HasForeignKey(x => x.TemplateId)
         .OnDelete(DeleteBehavior.SetNull);

        // The string values inside the CHECK must match the C# enum names EXACTLY ('GradeColumn',
        // 'FinalTotal'). The project's snake_case convention applies to table and column names,
        // NOT to the values stored in the cells. Write 'grade_column' here and the migration still
        // applies cleanly while every INSERT is rejected — discovered when the first bonus point
        // is granted, typically live in class between two group presentations.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bonus_target_column", """
                (target = 'GradeColumn' AND target_column_id IS NOT NULL) OR
                (target = 'FinalTotal'  AND target_column_id IS NULL)
                """);
            t.HasCheckConstraint("ck_bonus_points_positive", "points > 0");
            t.HasCheckConstraint("ck_bonus_reason_not_blank", "btrim(reason) <> ''");
        });

        b.HasIndex(x => new { x.EnrollmentId, x.GrantedAt });
        b.HasIndex(x => x.OfferingId);
    }
}

public sealed class GradeExportTemplateConfiguration : IEntityTypeConfiguration<GradeExportTemplate>
{
    public void Configure(EntityTypeBuilder<GradeExportTemplate> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TemplateId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.InstructorId, x.Name }).IsUnique();
    }
}

public sealed class GradeExportItemConfiguration : IEntityTypeConfiguration<GradeExportItem>
{
    public void Configure(EntityTypeBuilder<GradeExportItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ColumnKey).HasMaxLength(64);
        b.Property(x => x.Header).HasMaxLength(200).IsRequired();

        b.HasIndex(x => new { x.TemplateId, x.OrderIndex });
    }
}

public sealed class GradeSnapshotConfiguration : IEntityTypeConfiguration<GradeSnapshot>
{
    public void Configure(EntityTypeBuilder<GradeSnapshot> b)
    {
        b.HasKey(x => x.Id);

        // TEXT and NOT jsonb, and this is the only column in the project deliberately like that.
        // Postgres normalises jsonb on write: it strips whitespace and REORDERS THE KEYS. The
        // string read back differs from the string written in, so a SHA-256 recomputed over the
        // value read back will not match payload_hash — and being able to verify it is the entire
        // reason snapshots exist (ADR-006). Nobody queries inside a snapshot, so jsonb's query
        // capability does not make up for that loss.
        b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();

        // Restrict: a grade snapshot is never deleted along with anything else, not even when the
        // class offering is deleted.
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.OfferingId, x.TakenAt });
    }
}
