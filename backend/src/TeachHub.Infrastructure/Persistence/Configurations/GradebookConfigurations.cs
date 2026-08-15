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

        // Aggregation CHỨA enum Kind → phải khai .HasConversion<string>() cho riêng nó.
        // EF dùng JsonValueReaderWriter cho cột ToJson() và KHÔNG đọc JsonStringEnumConverter
        // của tầng HTTP. Thiếu dòng này thì jsonb lưu {"Kind": 6} — không lỗi, không test đỏ,
        // nhưng mọi truy vấn JSON theo tên enum về sau đều trượt im lặng, và ngày ai đó chèn
        // một giá trị vào giữa enum thì toàn bộ dữ liệu cũ đổi nghĩa.
        b.OwnsOne(x => x.Aggregation, a =>
        {
            a.ToJson();
            a.Property(y => y.Kind).HasConversion<string>();
        });

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Sources).WithOne().HasForeignKey(s => s.GradeColumnId)
         .OnDelete(DeleteBehavior.Cascade);

        // Ràng buộc PHẢI ở database chứ không chỉ ở validator: cột còn được sinh ra bởi việc
        // sao chép mẫu và bởi thao tác "áp mẫu khác" — cả hai đều không đi qua màn hình cấu hình.
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

        // source_id trỏ tới Assignment HOẶC Quiz HOẶC FinalAssessment tuỳ SourceType,
        // nên cố ý KHÔNG khai FK — nhất quán với LessonComponent.RefId của Phase 1.
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

        // MỖI nơi có property kiểu GradeAggregation đều cần MỘT dòng OwnsOne riêng — EF không
        // suy ra được từ cấu hình của entity khác. Thiếu dòng này thì mô hình EF không dựng
        // được và ứng dụng không khởi động nổi, với một thông báo lỗi không chỉ ra thiếu ở đâu.
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

        // Giá trị chuỗi trong CHECK phải khớp ĐÚNG tên enum C# ('GradeColumn', 'FinalTotal').
        // Quy ước snake_case của dự án áp cho tên bảng và tên cột, KHÔNG áp cho giá trị trong ô.
        // Viết 'grade_column' ở đây thì migration vẫn chạy trót lọt và mọi INSERT bị chặn —
        // phát hiện lúc cấp điểm cộng đầu tiên, thường là trên lớp giữa hai nhóm báo cáo.
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
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();

        // Restrict: bản chốt điểm không bao giờ bị xóa theo, kể cả khi lớp bị xóa.
        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.OfferingId, x.TakenAt });
    }
}
