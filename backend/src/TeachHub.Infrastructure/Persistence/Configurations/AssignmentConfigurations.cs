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

        // Gap #1: instructor_id là FK thật, không phải Guid trôi nổi.
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Criteria).WithOne().HasForeignKey(c => c.RubricId)
         .OnDelete(DeleteBehavior.Cascade);

        // MaxPoints là property tính toán từ Criteria — không có cột tương ứng.
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

        // Các mức mô tả → jsonb. CriterionLevel không chứa enum nào; ngày nào thêm enum
        // vào đây thì phải khai .HasConversion<string>() cho riêng nó — EF dùng
        // JsonValueReaderWriter cho cột ToJson() và KHÔNG đọc JsonStringEnumConverter.
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

        // final_assessment_id chưa có bảng đích: FinalAssessment thuộc Phase 4a. Cột và
        // CHECK constraint dựng sẵn từ bây giờ để Phase 4a chỉ phải thêm đúng một FK,
        // không phải migrate lại một bảng đã có bài nộp thật.
        b.OwnsOne(x => x.RequiredStructure, r => r.ToJson());

        b.Ignore(x => x.AcceptsZip);
        b.Ignore(x => x.MaxFileSizeBytes);

        // Giá trị trong CHECK phải khớp ĐÚNG tên enum C# vì cột lưu chuỗi qua
        // HasConversion<string>(). Viết 'assignment' snake_case ở đây thì migration vẫn
        // chạy trót lọt nhưng MỌI lệnh INSERT bị chặn — và chỉ lộ ra lúc tạo slot đầu tiên.
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
         .OnDelete(DeleteBehavior.Restrict);   // KHÔNG cascade: bài nộp là bằng chứng chấm điểm
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

        // rubric_scores_json: Dictionary không phải primitive collection mà EF map sẵn,
        // nên đi qua converter tường minh. ValueComparer là BẮT BUỘC — thiếu nó thì EF so
        // sánh Dictionary bằng tham chiếu, không phát hiện thay đổi nội dung, và điểm rubric
        // sửa xong không được ghi xuống DB mà cũng không có lỗi nào.
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

        // Bất biến "mỗi người nộp chỉ có đúng MỘT bản hiện hành" do database giữ, không do
        // handler nhớ. Thiếu index này thì một cú double-click nút "Nộp chính thức" tạo hai
        // hàng is_current, và sổ điểm lấy Single() sẽ ném exception giữa buổi chấm.
        // HasFilter nhận SQL THÔ nên tên cột viết snake_case.
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
        b.Property(x => x.ExternalUrl).HasMaxLength(1000);
        b.Property(x => x.ContentType).HasMaxLength(200);
        b.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.MissingStructureItems).HasColumnType("text[]");

        b.HasOne(x => x.Slot).WithMany()
         .HasForeignKey(x => x.SlotId).OnDelete(DeleteBehavior.Restrict);

        // file_tree_json → jsonb có kiểu. Danh sách complex LỒNG bên trong cột ToJson()
        // phải khai tường minh bằng OwnsMany, EF không suy ra được.
        b.OwnsOne(x => x.FileTree, t =>
        {
            t.ToJson();
            t.OwnsMany(y => y.Files);
        });

        b.Ignore(x => x.IsLink);

        b.HasIndex(x => new { x.SubmissionId, x.SlotKey });
    }
}
