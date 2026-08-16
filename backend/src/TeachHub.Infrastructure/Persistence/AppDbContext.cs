using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Assessments;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;
using TeachHub.Domain.Content;
using TeachHub.Domain.Gamification;
using TeachHub.Domain.Integrity;
using TeachHub.Domain.Review;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Groups;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Learning;
using TeachHub.Domain.Quizzes;
using TeachHub.Domain.Submissions;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InstructorInvite> InstructorInvites => Set<InstructorInvite>();

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectVersion> SubjectVersions => Set<SubjectVersion>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonComponent> LessonComponents => Set<LessonComponent>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();

    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    public DbSet<ComponentProgress> ComponentProgresses => Set<ComponentProgress>();

    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<AssignmentHint> AssignmentHints => Set<AssignmentHint>();
    public DbSet<SubmissionSlot> SubmissionSlots => Set<SubmissionSlot>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();

    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GradeAuditLog> GradeAuditLogs => Set<GradeAuditLog>();
    public DbSet<FinalAssessment> FinalAssessments => Set<FinalAssessment>();

    public DbSet<GradeColumn> GradeColumns => Set<GradeColumn>();
    public DbSet<GradeColumnSource> GradeColumnSources => Set<GradeColumnSource>();
    public DbSet<GradeRecord> GradeRecords => Set<GradeRecord>();
    public DbSet<GradeSchemeTemplate> GradeSchemeTemplates => Set<GradeSchemeTemplate>();
    public DbSet<GradeSchemeTemplateItem> GradeSchemeTemplateItems => Set<GradeSchemeTemplateItem>();
    public DbSet<BonusPoint> BonusPoints => Set<BonusPoint>();
    public DbSet<BonusPointReasonTemplate> BonusPointReasonTemplates => Set<BonusPointReasonTemplate>();
    public DbSet<GradeExportTemplate> GradeExportTemplates => Set<GradeExportTemplate>();
    public DbSet<GradeExportItem> GradeExportItems => Set<GradeExportItem>();
    public DbSet<GradeSnapshot> GradeSnapshots => Set<GradeSnapshot>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuizItem> QuizItems => Set<QuizItem>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    public DbSet<SourceFingerprint> SourceFingerprints => Set<SourceFingerprint>();
    public DbSet<HintUnlock> HintUnlocks => Set<HintUnlock>();
    public DbSet<SubmissionMemberScore> SubmissionMemberScores => Set<SubmissionMemberScore>();

    public DbSet<XpRule> XpRules => Set<XpRule>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<EarnedBadge> EarnedBadges => Set<EarnedBadge>();
    public DbSet<OfferingGamificationSettings> OfferingGamificationSettings => Set<OfferingGamificationSettings>();

    public DbSet<ReviewQueueItem> ReviewQueueItems => Set<ReviewQueueItem>();
    public DbSet<ReviewGenerationMark> ReviewGenerationMarks => Set<ReviewGenerationMark>();

    DbSet<AppUser> IAppDbContext.Users => Users;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ASP.NET Core Identity names its 7 tables EXPLICITLY via ToTable("AspNetUsers"), and
        // explicit configuration always beats convention. That is why UseSnakeCaseNamingConvention()
        // does rename columns, primary keys and foreign keys, but does NOT rename tables — the
        // schema would end up mixing "AspNetUsers" with a "normalized_email" column. Rename those
        // exact 7 tables here so the whole schema stays consistently snake_case (architecture
        // document, convention §5).
        b.Entity<AppUser>().ToTable("asp_net_users");
        b.Entity<AppRole>().ToTable("asp_net_roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");
        b.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");

        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        foreach (var e in ChangeTracker.Entries<IAuditable>())
        {
            if (e.State == EntityState.Added)    e.Entity.CreatedAt = now;
            if (e.State == EntityState.Modified) e.Entity.UpdatedAt = now;
        }
        foreach (var e in ChangeTracker.Entries<ISoftDeletable>().Where(x => x.State == EntityState.Deleted))
        {
            e.State = EntityState.Modified;      // soft delete — never DELETE teaching content
            e.Entity.IsDeleted = true;
            e.Entity.DeletedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }
}
