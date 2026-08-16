using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Integrity;
using TeachHub.Domain.Submissions;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Dấu vân tay Winnowing (Gap #14). Bảng lớn nhất của hệ thống tính theo số dòng — một lớp
/// 40 người nộp một bài 500 dòng sinh khoảng 60 000 dòng ở đây — nên chỉ chứa số, không chứa
/// chuỗi nào ngoài đường dẫn entry.
/// </summary>
public sealed class SourceFingerprintConfiguration : IEntityTypeConfiguration<SourceFingerprint>
{
    public void Configure(EntityTypeBuilder<SourceFingerprint> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.EntryPath).HasMaxLength(1024);

        // Cascade: xóa tệp nộp thì dấu vân tay của nó phải đi theo. Giữ lại là để rác trỏ vào
        // hư không, và báo cáo trùng lặp sẽ so bài hiện tại với một tệp không còn tồn tại.
        b.HasOne<SubmissionFile>().WithMany()
         .HasForeignKey(x => x.SubmissionFileId).OnDelete(DeleteBehavior.Cascade);

        // Index trên hash — Gap #14 nêu đích danh. Không có nó thì mỗi lần mở báo cáo trùng
        // lặp là một lần quét toàn bảng vài chục triệu dòng.
        b.HasIndex(x => x.Hash);

        // Index phụ theo tệp: xóa và tính lại dấu vân tay của MỘT tệp (nộp lại bài) phải chạm
        // đúng phần của tệp đó, không quét cả bảng.
        b.HasIndex(x => x.SubmissionFileId);
    }
}

/// <summary>Nhật ký mở gợi ý (Gap #11).</summary>
public sealed class HintUnlockConfiguration : IEntityTypeConfiguration<HintUnlock>
{
    public void Configure(EntityTypeBuilder<HintUnlock> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PenaltyPercentApplied).HasPrecision(5, 2);

        b.HasOne<AssignmentHint>().WithMany()
         .HasForeignKey(x => x.AssignmentHintId).OnDelete(DeleteBehavior.Cascade);

        // Restrict: nhật ký mở gợi ý là căn cứ trừ điểm của sinh viên. Dọn theo khi sửa lớp
        // thì điểm đã trừ không còn giải thích được, và đó là dữ liệu điểm chứ không phải
        // dữ liệu nội dung dạy học.
        b.HasOne<Enrollment>().WithMany()
         .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);

        // Mở hai lần KHÔNG được trừ hai lần. Bất biến này phải do database giữ: handler chỉ
        // cần một lần double-click nút "Mở gợi ý" là ghi hai hàng, và sinh viên mất 20% điểm
        // cho một gợi ý duy nhất mà không ai lần ra vì sao.
        b.HasIndex(x => new { x.AssignmentHintId, x.EnrollmentId }).IsUnique();
    }
}

/// <summary>
/// Bốn cột Phase 6b thêm vào <see cref="Submission"/>. Khai trong một
/// <c>IEntityTypeConfiguration&lt;Submission&gt;</c> RIÊNG chứ không sửa cấu hình cũ:
/// <c>ApplyConfigurationsFromAssembly</c> gom mọi cấu hình của cùng một entity, nên hai lớp
/// bổ sung cho nhau. Nhờ vậy toàn bộ diff của phase này nằm gọn trong một file và có thể
/// đọc lại được khi truy nguyên một migration.
/// </summary>
public sealed class SubmissionPenaltyConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        // Cùng decimal(5,2) với Score. Thiếu HasPrecision thì Npgsql tạo numeric không giới
        // hạn — vẫn chạy, nhưng lệch với phần còn lại của schema và làm diff migration lần
        // sau ồn ào vì một cột duy nhất.
        b.Property(x => x.RawScore).HasPrecision(5, 2);
        b.Property(x => x.LatePenaltyPercent).HasPrecision(5, 2);
        b.Property(x => x.HintPenaltyPercent).HasPrecision(5, 2);
    }
}

/// <summary>Điểm riêng từng thành viên nhóm (UC-10b).</summary>
public sealed class SubmissionMemberScoreConfiguration
    : IEntityTypeConfiguration<SubmissionMemberScore>
{
    public void Configure(EntityTypeBuilder<SubmissionMemberScore> b)
    {
        b.HasKey(x => x.Id);

        // decimal(5,2) như mọi cột điểm khác. double ở đây làm 0.1 + 0.2 ≠ 0.3 và điểm tổng
        // lệch ở chữ số thập phân thứ hai — sinh viên cộng tay lại sẽ ra số khác.
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();

        b.HasOne<Submission>().WithMany(s => s.MemberScores)
         .HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Enrollment>().WithMany()
         .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);

        // Một thành viên có ĐÚNG một điểm riêng cho một bài nộp. Hai hàng thì
        // MemberScoring.Resolve lấy hàng nào là chuyện may rủi theo thứ tự đọc của Postgres.
        b.HasIndex(x => new { x.SubmissionId, x.EnrollmentId }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint(
            "ck_submission_member_scores_range",
            "score >= 0 AND score <= 10"));

        b.ToTable(t => t.HasCheckConstraint(
            "ck_submission_member_scores_reason_not_blank",
            "length(btrim(reason)) > 0"));
    }
}
