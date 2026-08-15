using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Learning;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOffering>
{
    public void Configure(EntityTypeBuilder<CourseOffering> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Semester).HasMaxLength(50).IsRequired();
        b.Property(x => x.ClassCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.JoinCodeValue).HasMaxLength(12);
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId)
         .OnDelete(DeleteBehavior.Restrict);

        // Restrict chứ không Cascade: xoá một SubjectVersion mà kéo theo cả lớp đã dạy
        // là mất sạch điểm và bài nộp của học kỳ đó.
        b.HasOne<SubjectVersion>().WithMany().HasForeignKey(x => x.SubjectVersionId)
         .OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.BonusDefaultTarget).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.BonusMaxPointsPerStudent).HasPrecision(5, 2);

        // GradeCondition chứa HAI enum (Op, Action) → phải khai .HasConversion<string>() cho
        // TỪNG cái. EF dùng JsonValueReaderWriter cho cột ToJson() và không đọc cấu hình JSON
        // của tầng HTTP; thiếu là điều kiện điểm sàn lưu số trong jsonb và đổi nghĩa khi enum
        // được chèn thêm giá trị.
        b.OwnsMany(x => x.GradeConditions, c =>
        {
            c.ToJson();
            c.Property(y => y.Op).HasConversion<string>();
            c.Property(y => y.Action).HasConversion<string>();
        });

        b.HasIndex(x => new { x.SubjectId, x.Semester, x.ClassCode }).IsUnique();

        // Mã lớp phải duy nhất trong số các mã CÒN HIỆU LỰC; mã cũ hết hạn được tái sử dụng.
        b.HasIndex(x => x.JoinCodeValue).IsUnique().HasFilter("join_code_value IS NOT NULL");
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.StudentCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.ClassName).HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.SetNull);

        // Đây là thứ làm cho "import lại cùng file" không tạo bản trùng: handler upsert
        // theo cặp này thay vì kiểm-rồi-chèn.
        b.HasIndex(x => new { x.OfferingId, x.StudentCode }).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId)
         .OnDelete(DeleteBehavior.Cascade);

        // Bất biến do DATABASE giữ, không do handler nhớ: sinh viên bấm nhanh hai lần là
        // hai transaction song song, và "kiểm rồi chèn" trong handler không thấy nhau.
        b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
    }
}

public sealed class ComponentProgressConfiguration : IEntityTypeConfiguration<ComponentProgress>
{
    public void Configure(EntityTypeBuilder<ComponentProgress> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<LessonComponent>().WithMany().HasForeignKey(x => x.LessonComponentId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EnrollmentId, x.LessonComponentId }).IsUnique();
    }
}
