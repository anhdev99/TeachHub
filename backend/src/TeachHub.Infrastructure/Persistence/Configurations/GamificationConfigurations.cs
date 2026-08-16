using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachHub.Domain.Gamification;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Infrastructure.Persistence.Configurations;

public sealed class XpRuleConfiguration : IEntityTypeConfiguration<XpRule>
{
    public void Configure(EntityTypeBuilder<XpRule> b)
    {
        b.HasKey(x => x.Id);

        // An enum going down to the DB is ALWAYS a string. EF maps to integer by default, and
        // then every CHECK written with strings dies the moment the migration is applied,
        // because it compares integer = text.
        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);

        // MinValue is compared against quiz scores (the 0–10 scale) AND against streak day counts,
        // so it is NOT clamped to [0, 10].
        b.Property(x => x.MinValue).HasPrecision(5, 2);

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_xp_rules_points_positive", "points > 0");
            t.HasCheckConstraint("ck_xp_rules_min_value_not_negative",
                "min_value IS NULL OR min_value >= 0");
            t.HasCheckConstraint("ck_xp_rules_daily_cap_positive",
                "daily_cap IS NULL OR daily_cap > 0");
        });

        b.HasIndex(x => new { x.OfferingId, x.EventType });
    }
}

public sealed class XpEventConfiguration : IEntityTypeConfiguration<XpEvent>
{
    public void Configure(EntityTypeBuilder<XpEvent> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DedupeKey).HasMaxLength(128).IsRequired();

        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);

        // The anti-double-award invariant has to be held by the DATABASE, not by the handler
        // remembering it. Without this unique index every re-sync awards the points again, and
        // the "mark as done, then un-mark it" trick becomes an infinite XP printer — the number
        // one hole in every gamification system.
        b.HasIndex(x => new { x.EnrollmentId, x.DedupeKey }).IsUnique();

        b.HasIndex(x => new { x.EnrollmentId, x.OccurredAt });

        b.ToTable(t => t.HasCheckConstraint("ck_xp_events_points_positive", "points > 0"));
    }
}

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Icon).HasMaxLength(16);
        b.Property(x => x.Logic).HasConversion<string>().HasMaxLength(32);

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InstructorId)
         .OnDelete(DeleteBehavior.Cascade);

        // Conditions is a jsonb column containing TWO enums. EF uses a dedicated
        // JsonValueReaderWriter for a ToJson() column and does NOT read the HTTP layer's
        // JsonStringEnumConverter, so EACH enum needs its own .HasConversion<string>(). Miss one
        // line and the jsonb stores {"Metric": 5}: no error, no failing test, but the day someone
        // inserts a value into the middle of BadgeMetric, every stored badge condition changes
        // meaning — "reach 100 XP" becomes "submit late 100 times".
        b.OwnsMany(x => x.Conditions, c =>
        {
            c.ToJson();
            c.Property(y => y.Metric).HasConversion<string>();
            c.Property(y => y.Comparison).HasConversion<string>();
        });

        b.HasIndex(x => new { x.InstructorId, x.Code }).IsUnique();

        // Same shape as ck_grade_column_key_slug: the code is a stable slug the UI looks up by.
        b.ToTable(t => t.HasCheckConstraint("ck_badges_code_slug", "code ~ '^[a-z0-9_]+$'"));
    }
}

public sealed class EarnedBadgeConfiguration : IEntityTypeConfiguration<EarnedBadge>
{
    public void Configure(EntityTypeBuilder<EarnedBadge> b)
    {
        b.HasKey(x => x.Id);

        b.HasOne<Badge>().WithMany().HasForeignKey(x => x.BadgeId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId)
         .OnDelete(DeleteBehavior.Cascade);

        // Once awarded, never awarded again. Granting the same badge twice is the kind of display
        // bug a student screenshots and sends immediately, and it also throws off every badge
        // count that follows.
        b.HasIndex(x => new { x.EnrollmentId, x.BadgeId }).IsUnique();
    }
}

public sealed class OfferingGamificationSettingsConfiguration
    : IEntityTypeConfiguration<OfferingGamificationSettings>
{
    public void Configure(EntityTypeBuilder<OfferingGamificationSettings> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.LeaderboardVisibility).HasConversion<string>().HasMaxLength(32);

        b.HasOne<CourseOffering>().WithMany().HasForeignKey(x => x.OfferingId)
         .OnDelete(DeleteBehavior.Cascade);

        // A class offering has exactly ONE settings row. Without the unique index, hitting save
        // twice creates two rows, and which one the class actually runs on depends on the query —
        // including the flag that reveals students' real names.
        b.HasIndex(x => x.OfferingId).IsUnique();

        b.ToTable(t => t.HasCheckConstraint(
            "ck_offering_gamification_leaderboard_size", "leaderboard_size > 0"));
    }
}
