using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();

        // Gap #1: instructor_id là FK thật tới bảng người dùng, không phải Guid trôi nổi.
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Restrict);

        // Mỗi giảng viên không được có hai môn trùng mã. Partial index vì bản xóa mềm
        // vẫn nằm lại trong bảng và không được chiếm chỗ mã đó.
        b.HasIndex(x => new { x.InstructorId, x.Code })
         .IsUnique()
         .HasFilter("NOT is_deleted");   // SQL thô: tên cột snake_case

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

        // syllabus_json của SRS → jsonb có kiểu, không phải chuỗi.
        // Danh sách complex LỒNG BÊN TRONG cột ToJson() phải khai tường minh bằng OwnsMany:
        // EF không suy ra được và mô hình sẽ không dựng nổi ("Unable to determine the
        // relationship represented by navigation 'Syllabus.Outcomes'"). List<string> thì
        // không cần vì đó là primitive collection.
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

        // config_json của SRS → jsonb có kiểu.
        // ComponentConfig KHÔNG chứa enum nào; ngày nào thêm enum vào đây thì phải khai
        // .HasConversion<string>() cho riêng nó — EF dùng JsonValueReaderWriter riêng cho
        // cột ToJson() và KHÔNG đọc JsonStringEnumConverter của ASP.NET Core.
        b.OwnsOne(x => x.Config, c =>
        {
            c.ToJson();
            c.OwnsMany(cfg => cfg.InlineQuestions);   // xem ghi chú ở SubjectVersionConfiguration
        });

        // RefId cố ý KHÔNG khai FK: nó trỏ tới Assignment HOẶC Quiz tuỳ Type.
        // Ràng buộc nhất quán do SubjectVersionCloner và validator giữ.
        b.HasIndex(x => new { x.LessonId, x.OrderIndex });
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();

        b.HasOne<SubjectVersion>().WithMany().HasForeignKey(x => x.SubjectVersionId)
         .OnDelete(DeleteBehavior.Cascade);

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

        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.SubjectVersionId);
    }
}
