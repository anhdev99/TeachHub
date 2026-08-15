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

        // Một sinh viên không thể ở hai nhóm cùng lúc trong cùng một lớp. Enrollment đã gắn
        // cứng vào một lớp nên unique trên enrollment_id là đủ — không cần lặp offering_id.
        // Để database giữ bất biến này chứ không để handler nhớ: hai request song song
        // "thêm vào nhóm" sẽ không cùng nhìn thấy nhau.
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

        // Restrict ở mọi phía: nhật ký điểm KHÔNG BAO GIỜ bị xóa theo, kể cả khi lớp bị xóa.
        // Nó tồn tại chính vì những lúc dữ liệu gốc không còn.
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

        // GroupConfig chứa enum AllocationMode → phải khai .HasConversion<string>() cho riêng
        // nó. EF dùng JsonValueReaderWriter cho cột ToJson() và KHÔNG đọc JsonStringEnumConverter
        // cấu hình ở tầng HTTP; thiếu dòng này thì jsonb lưu số và dữ liệu đổi nghĩa vào ngày
        // ai đó chèn một giá trị vào giữa enum.
        b.OwnsOne(x => x.GroupConfig, g =>
        {
            g.ToJson();
            g.Property(y => y.AllocationMode).HasConversion<string>();
        });

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Rubric>().WithMany().HasForeignKey(x => x.RubricId)
         .OnDelete(DeleteBehavior.Restrict);

        // grade_column_id chưa có FK: bảng grade_columns thuộc Phase 4b, sẽ thêm ràng buộc ở đó.
        b.HasMany(x => x.Slots).WithOne().HasForeignKey(s => s.FinalAssessmentId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OfferingId);
    }
}
