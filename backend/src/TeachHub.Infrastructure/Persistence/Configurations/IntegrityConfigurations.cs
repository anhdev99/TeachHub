using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Integrity;
using TeachHub.Domain.Submissions;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Winnowing fingerprints (Gap #14). The largest table in the system by row count — a class of
/// 40 students submitting a 500-line assignment produces roughly 60,000 rows here — so it holds
/// only numbers, with no strings other than the entry path.
/// </summary>
public sealed class SourceFingerprintConfiguration : IEntityTypeConfiguration<SourceFingerprint>
{
    public void Configure(EntityTypeBuilder<SourceFingerprint> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.EntryPath).HasMaxLength(1024);

        // Cascade: delete a submission file and its fingerprints must go with it. Keeping them
        // leaves garbage pointing at nothing, and the similarity report would compare the current
        // work against a file that no longer exists.
        b.HasOne<SubmissionFile>().WithMany()
         .HasForeignKey(x => x.SubmissionFileId).OnDelete(DeleteBehavior.Cascade);

        // Index on the hash — Gap #14 calls this out by name. Without it, every time the
        // similarity report is opened it means a full scan of a table with tens of millions
        // of rows.
        b.HasIndex(x => x.Hash);

        // Secondary index by file: deleting and recomputing the fingerprints of ONE file (a
        // resubmission) must touch only that file's rows, not scan the whole table.
        b.HasIndex(x => x.SubmissionFileId);
    }
}

/// <summary>The hint unlock log (Gap #11).</summary>
public sealed class HintUnlockConfiguration : IEntityTypeConfiguration<HintUnlock>
{
    public void Configure(EntityTypeBuilder<HintUnlock> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PenaltyPercentApplied).HasPrecision(5, 2);

        b.HasOne<AssignmentHint>().WithMany()
         .HasForeignKey(x => x.AssignmentHintId).OnDelete(DeleteBehavior.Cascade);

        // Restrict: the hint unlock log is the justification for a student's grade deduction.
        // Cleaning it up along with an edit to the class offering leaves an applied deduction
        // that can no longer be explained — and this is grade data, not teaching content.
        b.HasOne<Enrollment>().WithMany()
         .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);

        // Unlocking twice must NOT deduct twice. This invariant has to be held by the database:
        // one double-click on the "unlock hint" button is all it takes for the handler to write
        // two rows, and the student loses 20% of the score for a single hint with nobody able to
        // trace why.
        b.HasIndex(x => new { x.AssignmentHintId, x.EnrollmentId }).IsUnique();
    }
}

/// <summary>
/// The four columns Phase 6b adds to <see cref="Submission"/>. Declared in a SEPARATE
/// <c>IEntityTypeConfiguration&lt;Submission&gt;</c> rather than by editing the existing
/// configuration: <c>ApplyConfigurationsFromAssembly</c> collects every configuration for the
/// same entity, so the two classes complement each other. That keeps this phase's whole diff in
/// one file, where it can be read back when tracing a migration.
/// </summary>
public sealed class SubmissionPenaltyConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        // The same decimal(5,2) as Score. Without HasPrecision, Npgsql creates an unconstrained
        // numeric — it still works, but it diverges from the rest of the schema and makes the
        // next migration diff noisy over a single column.
        b.Property(x => x.RawScore).HasPrecision(5, 2);
        b.Property(x => x.LatePenaltyPercent).HasPrecision(5, 2);
        b.Property(x => x.HintPenaltyPercent).HasPrecision(5, 2);
    }
}

/// <summary>Per-member scores within a group (UC-10b).</summary>
public sealed class SubmissionMemberScoreConfiguration
    : IEntityTypeConfiguration<SubmissionMemberScore>
{
    public void Configure(EntityTypeBuilder<SubmissionMemberScore> b)
    {
        b.HasKey(x => x.Id);

        // decimal(5,2) like every other score column. A double here makes 0.1 + 0.2 ≠ 0.3 and the
        // total drifts at the second decimal place — a student adding it up by hand gets a
        // different number.
        b.Property(x => x.Score).HasPrecision(5, 2);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();

        b.HasOne<Submission>().WithMany(s => s.MemberScores)
         .HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Enrollment>().WithMany()
         .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);

        // One member has EXACTLY one per-member score for one submission. With two rows, which
        // one MemberScoring.Resolve picks is down to luck and Postgres's read order.
        b.HasIndex(x => new { x.SubmissionId, x.EnrollmentId }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint(
            "ck_submission_member_scores_range",
            "score >= 0 AND score <= 10"));

        b.ToTable(t => t.HasCheckConstraint(
            "ck_submission_member_scores_reason_not_blank",
            "length(btrim(reason)) > 0"));
    }
}
