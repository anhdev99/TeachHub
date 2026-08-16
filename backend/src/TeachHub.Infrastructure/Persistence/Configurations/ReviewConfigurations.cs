using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Review;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class ReviewQueueItemConfiguration : IEntityTypeConfiguration<ReviewQueueItem>
{
    public void Configure(EntityTypeBuilder<ReviewQueueItem> b)
    {
        b.HasKey(x => x.Id);

        // Enum xuống DB LUÔN là chuỗi. Mặc định của EF là integer, và mọi CHECK constraint bên
        // dưới viết bằng chuỗi sẽ chết ngay lúc apply migration vì so sánh integer với text.
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // Gap #2: khóa ngoại trỏ thẳng tới NGƯỜI DÙNG, không đi qua enrollment. Đây là khác biệt
        // có chủ ý so với mọi bảng dữ liệu học tập khác của dự án — đừng "sửa cho nhất quán".
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Câu hỏi bị gỡ khỏi ngân hàng thì mục ôn tập của nó cũng hết nghĩa: không còn gì để hỏi.
        b.HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuestionId)
         .OnDelete(DeleteBehavior.Cascade);

        // SetNull chứ KHÔNG Cascade: đây chỉ là vết nguồn. Xóa một quiz cũ mà kéo theo hàng đợi
        // ôn tập là xóa lịch sử kiến thức của sinh viên vì một thao tác dọn dẹp của giảng viên.
        b.HasOne<QuizAttempt>().WithMany().HasForeignKey(x => x.SourceQuizAttemptId)
         .OnDelete(DeleteBehavior.SetNull);

        // Một người + một câu hỏi = đúng MỘT mục. Không có ràng buộc này thì mỗi lần sai lại
        // một câu sẽ đẻ thêm một mục, và sinh viên phải ôn cùng một câu năm lần trong một ngày.
        b.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();

        // Truy vấn "Ôn tập hôm nay" chạy mỗi lần sinh viên mở trang chủ. HasFilter nhận SQL THÔ:
        // tên cột phải snake_case, còn giá trị phải khớp ĐÚNG tên enum C# vì cột lưu chuỗi.
        b.HasIndex(x => new { x.UserId, x.NextReviewAt }).HasFilter("status = 'Pending'");

        // Job quét hằng ngày đếm theo hạn trên toàn hệ thống, không theo từng người.
        b.HasIndex(x => x.NextReviewAt).HasFilter("status = 'Pending'");

        // Bất biến do DATABASE giữ, không do handler nhớ: trạng thái, bậc và hạn ôn phải luôn
        // đi cùng nhau. Một hàng Status='Pending' mà NextReviewAt IS NULL sẽ nằm vĩnh viễn trong
        // hàng đợi: không bao giờ đến hạn, cũng không bao giờ biến mất, và không có lỗi nào báo.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_review_queue_items_stage_status", """
                (status = 'Pending'  AND stage <> 'Mastered' AND next_review_at IS NOT NULL) OR
                (status = 'Mastered' AND stage =  'Mastered' AND next_review_at IS NULL)
                """);

            t.HasCheckConstraint("ck_review_queue_items_counts_not_negative",
                "correct_count >= 0 AND wrong_count >= 0");
        });
    }
}

public sealed class ReviewGenerationMarkConfiguration : IEntityTypeConfiguration<ReviewGenerationMark>
{
    public void Configure(EntityTypeBuilder<ReviewGenerationMark> b)
    {
        b.HasKey(x => x.Id);

        b.HasOne<QuizAttempt>().WithMany().HasForeignKey(x => x.QuizAttemptId)
         .OnDelete(DeleteBehavior.Cascade);

        // Unique là toàn bộ lý do bảng này tồn tại — nó phải là lớp chặn CUỐI, không chỉ là một
        // phép kiểm trong handler. Hai lần chạy job chồng nhau (retry của Hangfire gối lên lần
        // chạy đang dở) sẽ cùng thấy "chưa có dấu" rồi cùng nạp; unique index bắt lần thứ hai
        // ngã ở SaveChanges và transaction của nó rollback trọn vẹn.
        b.HasIndex(x => x.QuizAttemptId).IsUnique();
    }
}
