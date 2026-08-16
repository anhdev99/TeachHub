using Shouldly;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Chấm nâng cao (Phase 6b): phạt nộp trễ, trừ điểm khi mở gợi ý, điểm riêng từng thành viên
/// nhóm. Đây là code tính điểm thật của sinh viên — mỗi ca biên dưới đây từng được plan nêu
/// đích danh vì sai một ca là sai điểm tổng kết của một con người.
/// </summary>
public class AdvancedScoringTests
{
    private static readonly DateTimeOffset Han =
        new(2026, 9, 20, 23, 59, 0, TimeSpan.FromHours(7));

    // ── Số ngày trễ ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Nop_dung_han_khong_tinh_tre_ngay_nao()
    {
        LatePenaltyCalculator.DaysLate(Han, Han).ShouldBe(0);
        LatePenaltyCalculator.DaysLate(Han, Han.AddHours(-3)).ShouldBe(0);
    }

    [Fact]
    public void Nop_tre_dung_1_phut_tinh_la_1_ngay()
    {
        // Làm tròn LÊN — trễ là trễ. Làm tròn xuống thì cả lớp nộp lúc 00:05 và không ai bị
        // phạt gì, tức là hạn nộp không còn ý nghĩa.
        LatePenaltyCalculator.DaysLate(Han, Han.AddMinutes(1)).ShouldBe(1);
        LatePenaltyCalculator.DaysLate(Han, Han.AddSeconds(1)).ShouldBe(1);
    }

    [Fact]
    public void Tre_dung_24_gio_van_la_1_ngay_chu_khong_phai_2()
    {
        // Ca này hỏng nếu tính bằng Math.Ceiling(TotalDays): TotalDays là double, và 24 giờ
        // chẵn có thể ra 1.0000000000000002 tùy phép trừ — lúc đó sinh viên nộp đúng 24 giờ
        // sau hạn bị phạt gấp đôi người nộp sau đó một giây. Phải tính bằng số nguyên ticks.
        LatePenaltyCalculator.DaysLate(Han, Han.AddDays(1)).ShouldBe(1);
        LatePenaltyCalculator.DaysLate(Han, Han.AddDays(1).AddSeconds(1)).ShouldBe(2);
        LatePenaltyCalculator.DaysLate(Han, Han.AddDays(3)).ShouldBe(3);
    }

    [Fact]
    public void Khong_co_han_nop_thi_khong_bao_gio_tre()
    {
        LatePenaltyCalculator.DaysLate(null, Han.AddDays(10)).ShouldBe(0);
        LatePenaltyCalculator.DaysLate(Han, null).ShouldBe(0);
    }

    [Fact]
    public void So_ngay_tre_khong_phu_thuoc_mui_gio_ghi_trong_moc_thoi_gian()
    {
        // Cùng một khoảnh khắc viết bằng hai offset khác nhau phải cho cùng kết quả. Không
        // thì hạn nộp 23:59 giờ Việt Nam bị hiểu khác đi tùy máy chủ đặt ở đâu.
        var nopVN = Han.AddHours(25);
        var nopUTC = nopVN.ToUniversalTime();

        LatePenaltyCalculator.DaysLate(Han, nopUTC)
            .ShouldBe(LatePenaltyCalculator.DaysLate(Han, nopVN));
    }

    // ── Phạt trễ ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Phat_tre_tru_dung_ty_le_phan_tram_moi_ngay()
    {
        var kq = LatePenaltyCalculator.Apply(10m, 10m, Han, Han.AddDays(2));

        kq.DaysLate.ShouldBe(2);
        kq.PercentDeducted.ShouldBe(20m);
        kq.FinalScore.ShouldBe(8m);
        kq.PenaltyPoints.ShouldBe(2m);
    }

    [Fact]
    public void Phat_tre_KHONG_BAO_GIO_day_diem_xuong_am()
    {
        // 30%/ngày × 5 ngày = 150%. Không kẹp thì điểm ra −5, và cột điểm âm sẽ kéo điểm
        // tổng kết của sinh viên xuống dưới cả những bài không nộp gì.
        var kq = LatePenaltyCalculator.Apply(10m, 30m, Han, Han.AddDays(5));

        kq.FinalScore.ShouldBe(0m);
        kq.PercentDeducted.ShouldBe(100m);      // báo cáo mức ĐÃ trừ thật, không báo 150%
        kq.PenaltyPoints.ShouldBe(10m);
    }

    [Fact]
    public void Phat_tre_bang_0_phan_tram_thi_khong_tru_gi()
    {
        LatePenaltyCalculator.Apply(9.5m, 0m, Han, Han.AddDays(4)).FinalScore.ShouldBe(9.5m);
    }

    [Fact]
    public void Nop_dung_han_thi_giu_nguyen_diem()
    {
        var kq = LatePenaltyCalculator.Apply(7.25m, 50m, Han, Han.AddMinutes(-1));

        kq.DaysLate.ShouldBe(0);
        kq.FinalScore.ShouldBe(7.25m);
    }

    [Fact]
    public void Diem_sau_phat_lam_tron_2_chu_so_thap_phan()
    {
        // 8.33 × 0.85 = 7.0805 → 7.08. Không làm tròn thì cột điểm decimal(5,2) tự cắt và
        // con số hiển thị lệch với con số dùng để tính điểm tổng — sinh viên cộng lại thấy sai.
        LatePenaltyCalculator.Apply(8.33m, 15m, Han, Han.AddHours(1)).FinalScore.ShouldBe(7.08m);
    }

    // ── Trừ điểm khi mở gợi ý ────────────────────────────────────────────────────────────

    [Fact]
    public void Khong_mo_goi_y_nao_thi_giu_nguyen_diem()
    {
        var kq = HintPenaltyCalculator.Apply(9m, []);

        kq.HintsUnlocked.ShouldBe(0);
        kq.PercentDeducted.ShouldBe(0m);
        kq.FinalScore.ShouldBe(9m);
    }

    [Fact]
    public void Mo_nhieu_goi_y_thi_cong_don_ty_le_tru()
    {
        var kq = HintPenaltyCalculator.Apply(10m, [10m, 15m]);

        kq.HintsUnlocked.ShouldBe(2);
        kq.PercentDeducted.ShouldBe(25m);
        kq.FinalScore.ShouldBe(7.5m);
    }

    [Fact]
    public void Goi_y_mien_phi_khong_tru_diem()
    {
        HintPenaltyCalculator.Apply(10m, [0m, 0m]).FinalScore.ShouldBe(10m);
    }

    [Fact]
    public void Tong_ty_le_tru_vuot_100_phan_tram_thi_diem_ve_0_chu_khong_am()
    {
        var kq = HintPenaltyCalculator.Apply(10m, [60m, 60m]);

        kq.PercentDeducted.ShouldBe(100m);
        kq.FinalScore.ShouldBe(0m);
    }

    // ── Gộp cả hai mức phạt ──────────────────────────────────────────────────────────────

    [Fact]
    public void Phat_goi_y_va_phat_tre_cong_huong_tren_diem_goc()
    {
        // 10 × (1 − 0.20) × (1 − 0.10) = 7.2
        var kq = SubmissionScoring.Apply(10m, 10m, Han, Han.AddDays(1), [20m]);

        kq.RawScore.ShouldBe(10m);
        kq.DaysLate.ShouldBe(1);
        kq.LatePenaltyPercent.ShouldBe(10m);
        kq.HintsUnlocked.ShouldBe(1);
        kq.HintPenaltyPercent.ShouldBe(20m);
        kq.FinalScore.ShouldBe(7.2m);
    }

    [Fact]
    public void Gop_hai_muc_phat_van_khong_bao_gio_ra_diem_am()
    {
        SubmissionScoring.Apply(10m, 80m, Han, Han.AddDays(3), [90m]).FinalScore.ShouldBe(0m);
    }

    [Fact]
    public void Ap_lai_nhieu_lan_tren_cung_diem_goc_cho_cung_ket_qua()
    {
        // Tính từ điểm GỐC chứ không tính chồng lên điểm đã phạt. Chồng lên thì mỗi lần
        // giảng viên bấm "tính lại" là điểm sinh viên tụt thêm một nấc, âm thầm.
        var lan1 = SubmissionScoring.Apply(10m, 10m, Han, Han.AddDays(1), [20m]);
        var lan2 = SubmissionScoring.Apply(lan1.RawScore, 10m, Han, Han.AddDays(1), [20m]);

        lan2.FinalScore.ShouldBe(lan1.FinalScore);
    }

    [Fact]
    public void Khong_tre_va_khong_goi_y_thi_diem_giu_nguyen_tuyet_doi()
    {
        SubmissionScoring.Apply(8.75m, 25m, Han, Han.AddHours(-1), []).FinalScore.ShouldBe(8.75m);
    }

    // ── Điểm riêng từng thành viên nhóm (UC-10b) ─────────────────────────────────────────

    private static readonly Guid An = Guid.CreateVersion7();
    private static readonly Guid Binh = Guid.CreateVersion7();
    private static readonly Guid Cuong = Guid.CreateVersion7();

    private static SubmissionMemberScore Rieng(Guid enrollmentId, decimal diem) => new()
    {
        SubmissionId = Guid.CreateVersion7(),
        EnrollmentId = enrollmentId,
        Score = diem,
        Reason = "Không tham gia phần lập trình.",
    };

    [Fact]
    public void Khong_co_diem_rieng_thi_ca_nhom_nhan_diem_chung()
    {
        var kq = MemberScoring.Resolve(8m, [An, Binh, Cuong], []);

        kq.Count.ShouldBe(3);
        kq.ShouldAllBe(m => m.Score == 8m && !m.IsOverridden);
    }

    [Fact]
    public void Diem_rieng_thanh_vien_THANG_THE_diem_chung()
    {
        var kq = MemberScoring.Resolve(8m, [An, Binh, Cuong], [Rieng(Binh, 5m)]);

        kq.Single(m => m.EnrollmentId == Binh).Score.ShouldBe(5m);
        kq.Single(m => m.EnrollmentId == Binh).IsOverridden.ShouldBeTrue();
        kq.Single(m => m.EnrollmentId == Binh).Reason.ShouldNotBeNullOrWhiteSpace();

        kq.Single(m => m.EnrollmentId == An).Score.ShouldBe(8m);
        kq.Single(m => m.EnrollmentId == An).IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void Diem_rieng_cao_hon_diem_chung_cung_thang_the()
    {
        // Thắng thế theo cả hai chiều: có nhóm mà một người gánh cả bài.
        MemberScoring.Resolve(6m, [An, Binh], [Rieng(An, 9m)])
            .Single(m => m.EnrollmentId == An).Score.ShouldBe(9m);
    }

    [Fact]
    public void Bai_chua_cham_ma_da_co_diem_rieng_thi_diem_rieng_van_hieu_luc()
    {
        var kq = MemberScoring.Resolve(null, [An, Binh], [Rieng(An, 4m)]);

        kq.Single(m => m.EnrollmentId == An).Score.ShouldBe(4m);
        kq.Single(m => m.EnrollmentId == Binh).Score.ShouldBeNull();
    }

    [Fact]
    public void Diem_rieng_cua_nguoi_khong_thuoc_nhom_bi_bo_qua()
    {
        // Dữ liệu rác còn sót sau khi giảng viên chuyển một sinh viên sang nhóm khác. Nhận
        // bừa thì bảng điểm hiện một dòng của người không có trong nhóm này.
        var kq = MemberScoring.Resolve(8m, [An, Binh], [Rieng(Cuong, 2m)]);

        kq.Count.ShouldBe(2);
        kq.ShouldNotContain(m => m.EnrollmentId == Cuong);
    }

    [Fact]
    public void Nhom_rong_khong_no()
    {
        MemberScoring.Resolve(8m, [], []).ShouldBeEmpty();
    }
}
