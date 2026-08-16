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

        // Enum xuống DB LUÔN là chuỗi. EF mặc định map ra integer, và khi đó mọi CHECK viết
        // bằng chuỗi chết ngay lúc apply migration vì so sánh integer = text.
        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);

        // MinValue so với điểm quiz (thang 10) LẪN số ngày streak, nên KHÔNG kẹp về [0, 10].
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

        // Bất biến chống cộng trùng phải do DATABASE giữ, không phải do handler nhớ. Không có
        // unique index này thì mỗi lần đồng bộ lại là một lần cộng thêm, và trò "đánh dấu xong
        // rồi bỏ đánh dấu" thành máy in XP vô hạn — lỗ hổng số một của mọi hệ gamification.
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

        // Conditions là cột jsonb chứa HAI enum. EF dùng JsonValueReaderWriter riêng cho cột
        // ToJson() và KHÔNG đọc JsonStringEnumConverter của tầng HTTP, nên phải khai
        // .HasConversion<string>() cho TỪNG enum. Thiếu một dòng thì jsonb lưu {"Metric": 5}:
        // không lỗi, không test đỏ, nhưng ngày ai đó chèn một giá trị vào giữa BadgeMetric là
        // toàn bộ điều kiện huy hiệu đã lưu đổi nghĩa — "đạt 100 XP" thành "nộp trễ 100 lần".
        b.OwnsMany(x => x.Conditions, c =>
        {
            c.ToJson();
            c.Property(y => y.Metric).HasConversion<string>();
            c.Property(y => y.Comparison).HasConversion<string>();
        });

        b.HasIndex(x => new { x.InstructorId, x.Code }).IsUnique();

        // Cùng khuôn với ck_grade_column_key_slug: mã là slug ổn định để giao diện tra cứu.
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

        // Đã trao thì không trao lại. Trao hai lần cùng một huy hiệu là lỗi hiển thị mà sinh
        // viên chụp màn hình gửi ngay, và nó cũng làm mọi phép đếm huy hiệu sai theo.
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

        // Một lớp có đúng MỘT cấu hình. Không có unique index thì hai lần bấm lưu tạo hai hàng,
        // và lớp chạy theo hàng nào là tùy câu truy vấn — kể cả cờ hiện tên thật.
        b.HasIndex(x => x.OfferingId).IsUnique();

        b.ToTable(t => t.HasCheckConstraint(
            "ck_offering_gamification_leaderboard_size", "leaderboard_size > 0"));
    }
}
