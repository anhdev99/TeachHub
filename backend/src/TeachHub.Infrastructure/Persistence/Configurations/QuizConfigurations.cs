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

        // FillBlankConfig contains no enum; AcceptedAnswers is a primitive collection, so EF maps
        // it straight into the jsonb column without needing OwnsMany.
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

        // A question appears only ONCE within the same quiz.
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

        // Restrict: an attempt is a student's grade data; it is not cleaned up along with an edit
        // to the class offering.
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Restrict);

        // Answers are stored as jsonb, updated after each question rather than waiting for submit.
        b.OwnsMany(x => x.Answers, a => a.ToJson());

        // Id lists stored as jsonb in the form of a primitive collection — no OwnsMany needed.
        b.Property(x => x.WrongQuestionIds).HasColumnType("jsonb");
        b.Property(x => x.QuestionOrder).HasColumnType("jsonb");

        b.HasIndex(x => new { x.QuizId, x.EnrollmentId, x.AttemptNo }).IsUnique();
        b.HasIndex(x => x.EnrollmentId);
    }
}
