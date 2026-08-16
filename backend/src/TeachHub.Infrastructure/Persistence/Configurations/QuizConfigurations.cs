using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Text).IsRequired();
        b.Property(x => x.Points).HasPrecision(5, 2);
        b.Property(x => x.FoundationTags).HasColumnType("text[]");

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Options).WithOne().HasForeignKey(o => o.QuestionId)
         .OnDelete(DeleteBehavior.Cascade);

        // FillBlankConfig không chứa enum; AcceptedAnswers là primitive collection nên EF map
        // thẳng vào cột jsonb mà không cần OwnsMany.
        b.OwnsOne(x => x.FillBlank, f => f.ToJson());

        b.HasIndex(x => x.InstructorId);
    }
}

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).IsRequired();
        b.HasIndex(x => new { x.QuestionId, x.OrderIndex });
    }
}

public sealed class QuizItemConfiguration : IEntityTypeConfiguration<QuizItem>
{
    public void Configure(EntityTypeBuilder<QuizItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PointsOverride).HasPrecision(5, 2);

        b.HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuestionId)
         .OnDelete(DeleteBehavior.Cascade);

        // Một câu hỏi chỉ xuất hiện MỘT lần trong cùng một quiz.
        b.HasIndex(x => new { x.QuizId, x.QuestionId }).IsUnique();
    }
}

public sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.RawScore).HasPrecision(5, 2);
        b.Property(x => x.MaxScore).HasPrecision(5, 2);
        b.Property(x => x.NormalizedScore).HasPrecision(5, 2);

        b.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId)
         .OnDelete(DeleteBehavior.Cascade);

        // Restrict: lượt làm là dữ liệu điểm của sinh viên, không bị dọn theo khi sửa lớp.
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Restrict);

        // Đáp án lưu jsonb, cập nhật sau mỗi câu chứ không đợi lúc nộp.
        b.OwnsMany(x => x.Answers, a => a.ToJson());

        // Danh sách id lưu jsonb dạng primitive collection — không cần OwnsMany.
        b.Property(x => x.WrongQuestionIds).HasColumnType("jsonb");
        b.Property(x => x.QuestionOrder).HasColumnType("jsonb");

        b.HasIndex(x => new { x.QuizId, x.EnrollmentId, x.AttemptNo }).IsUnique();
        b.HasIndex(x => x.EnrollmentId);
    }
}
