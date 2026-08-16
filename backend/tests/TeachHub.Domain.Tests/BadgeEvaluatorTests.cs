using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Điều kiện huy hiệu là DỮ LIỆU khai báo, không phải mã. Thêm một huy hiệu mới phải là thêm
/// một hàng trong bảng, không phải một lần deploy.
/// </summary>
public class BadgeEvaluatorTests
{
    private static BadgeCondition DK(BadgeMetric chiSo, decimal nguong,
                                     BadgeComparison phep = BadgeComparison.AtLeast) =>
        new() { Metric = chiSo, Threshold = nguong, Comparison = phep };

    private static BadgeSpec HuyHieu(string ma, BadgeLogic logic, params BadgeCondition[] dk) =>
        new(Guid.CreateVersion7(), ma, logic, dk);

    [Fact]
    public void Huy_hieu_KHONG_co_dieu_kien_nao_thi_KHONG_duoc_trao()
    {
        // Đóng theo hướng an toàn. Danh sách rỗng thỏa mãn "mọi điều kiện đều đúng" theo logic
        // hình thức, nên cách viết tự nhiên nhất (All(...)) sẽ trao huy hiệu đó cho TOÀN BỘ
        // sinh viên ngay khi giảng viên bấm lưu một huy hiệu còn dở dang.
        var trong = HuyHieu("chua_khai", BadgeLogic.All);

        BadgeEvaluator.IsEarned(trong, new LearnerMetrics { TotalXp = 999 }).ShouldBeFalse();
    }

    [Fact]
    public void Dat_du_XP_thi_duoc_huy_hieu()
    {
        var hh = HuyHieu("xp_100", BadgeLogic.All, DK(BadgeMetric.TotalXp, 100));

        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { TotalXp = 100 }).ShouldBeTrue();
        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { TotalXp = 99 }).ShouldBeFalse();
    }

    [Fact]
    public void Logic_All_doi_du_moi_dieu_kien()
    {
        var hh = HuyHieu("cham_chi", BadgeLogic.All,
            DK(BadgeMetric.TotalXp, 100), DK(BadgeMetric.CurrentStreakDays, 7));

        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { TotalXp = 500, CurrentStreakDays = 6 })
            .ShouldBeFalse();
        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { TotalXp = 500, CurrentStreakDays = 7 })
            .ShouldBeTrue();
    }

    [Fact]
    public void Logic_Any_chi_can_mot_dieu_kien_dung()
    {
        var hh = HuyHieu("mot_trong_hai", BadgeLogic.Any,
            DK(BadgeMetric.TotalXp, 1000), DK(BadgeMetric.LongestStreakDays, 5));

        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { TotalXp = 10, LongestStreakDays = 5 })
            .ShouldBeTrue();
    }

    [Fact]
    public void Streak_bay_ngay_lien_tiep_trao_huy_hieu()
    {
        var hh = HuyHieu("streak_7", BadgeLogic.All, DK(BadgeMetric.CurrentStreakDays, 7));

        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { CurrentStreakDays = 7 }).ShouldBeTrue();
    }

    [Fact]
    public void Nop_dung_han_ba_lan_lien_tiep_trao_huy_hieu()
    {
        var hh = HuyHieu("dung_han_3", BadgeLogic.All,
            DK(BadgeMetric.ConsecutiveOnTimeSubmissions, 3));

        var chiSo = new LearnerMetrics
        {
            ConsecutiveOnTimeSubmissions = BadgeEvaluator.CurrentOnTimeRun([true, true, true]),
        };

        BadgeEvaluator.IsEarned(hh, chiSo).ShouldBeTrue();
    }

    [Fact]
    public void Mot_lan_nop_tre_lam_dut_chuoi_dung_han()
    {
        // Thứ tự là thứ tự nộp theo thời gian. "Liên tiếp" tính tới lần nộp GẦN NHẤT, nên
        // một lần trễ ở giữa phải cắt chuỗi — nếu không thì "liên tiếp" chỉ còn nghĩa là "tổng".
        BadgeEvaluator.CurrentOnTimeRun([true, true, false, true]).ShouldBe(1);
        BadgeEvaluator.LongestOnTimeRun([true, true, false, true]).ShouldBe(2);
        BadgeEvaluator.CurrentOnTimeRun([]).ShouldBe(0);
        BadgeEvaluator.CurrentOnTimeRun([false]).ShouldBe(0);
    }

    [Fact]
    public void Phep_so_sanh_AtMost_dung_duoc_cho_huy_hieu_khong_bo_lo()
    {
        var hh = HuyHieu("khong_tre_lan_nao", BadgeLogic.All,
            DK(BadgeMetric.LateSubmissions, 0, BadgeComparison.AtMost),
            DK(BadgeMetric.OnTimeSubmissions, 5));

        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { OnTimeSubmissions = 5, LateSubmissions = 0 })
            .ShouldBeTrue();
        BadgeEvaluator.IsEarned(hh, new LearnerMetrics { OnTimeSubmissions = 5, LateSubmissions = 1 })
            .ShouldBeFalse();
    }

    [Fact]
    public void Huy_hieu_da_co_khong_duoc_trao_lai()
    {
        var da = HuyHieu("xp_100", BadgeLogic.All, DK(BadgeMetric.TotalXp, 100));
        var moi = HuyHieu("xp_200", BadgeLogic.All, DK(BadgeMetric.TotalXp, 200));

        var kq = BadgeEvaluator.NewlyEarned(
            [da, moi], new LearnerMetrics { TotalXp = 250 }, alreadyEarned: new HashSet<Guid> { da.Id });

        kq.Count.ShouldBe(1);
        kq[0].Code.ShouldBe("xp_200");
    }

    [Fact]
    public void Chua_dat_dieu_kien_thi_khong_co_gi_duoc_trao()
    {
        var hh = HuyHieu("xp_1000", BadgeLogic.All, DK(BadgeMetric.TotalXp, 1000));

        BadgeEvaluator.NewlyEarned([hh], new LearnerMetrics { TotalXp = 999 }, new HashSet<Guid>())
            .ShouldBeEmpty();
    }
}
