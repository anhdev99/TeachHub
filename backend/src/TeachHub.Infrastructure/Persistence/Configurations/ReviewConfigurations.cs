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

        // An enum going down to the DB is ALWAYS a string. EF's default is integer, and every
        // CHECK constraint below is written with strings, so they would die the moment the
        // migration is applied, comparing integer with text.
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // Gap #2: the foreign key points straight at the USER, not through an enrollment. This is
        // a deliberate difference from every other learning-data table in the project — do not
        // "fix it for consistency".
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Once a question is removed from the bank its review item is meaningless too: there is
        // nothing left to ask.
        b.HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuestionId)
         .OnDelete(DeleteBehavior.Cascade);

        // SetNull and NOT Cascade: this is only a provenance trace. Deleting an old quiz and
        // dragging the review queue with it would erase a student's knowledge history because of
        // an instructor's housekeeping.
        b.HasOne<QuizAttempt>().WithMany().HasForeignKey(x => x.SourceQuizAttemptId)
         .OnDelete(DeleteBehavior.SetNull);

        // One user + one question = exactly ONE item. Without this constraint, every time the same
        // question is answered wrong again it spawns another item, and the student has to review
        // the very same question five times in one day.
        b.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();

        // The "today's review" query runs every time a student opens the home page. HasFilter
        // takes RAW SQL: column names must be snake_case, and the values must match the C# enum
        // names EXACTLY, because the column stores strings.
        b.HasIndex(x => new { x.UserId, x.NextReviewAt }).HasFilter("status = 'Pending'");

        // The daily sweep job counts by due date across the whole system, not per user.
        b.HasIndex(x => x.NextReviewAt).HasFilter("status = 'Pending'");

        // An invariant held by the DATABASE, not by the handler remembering it: status, stage and
        // due date must always travel together. A row with Status='Pending' but NextReviewAt IS
        // NULL sits in the queue forever: never coming due, never going away, and never reporting
        // an error.
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

        // The unique index is the entire reason this table exists — it has to be the LAST line of
        // defence, not just a check inside the handler. Two overlapping job runs (a Hangfire retry
        // landing on top of a run still in progress) will both see "no mark yet" and both load;
        // the unique index makes the second one fail at SaveChanges, and its transaction rolls
        // back cleanly.
        b.HasIndex(x => x.QuizAttemptId).IsUnique();
    }
}
