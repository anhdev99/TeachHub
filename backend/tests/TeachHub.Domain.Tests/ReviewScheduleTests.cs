using Shouldly;
using TeachHub.Domain.Review;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Thang ôn tập ngắt quãng: 3 ngày → 1 tuần → 2 tuần → thuộc. Trả lời sai ở BẤT KỲ bậc nào
/// đều quay về đầu chu kỳ.
///
/// Đây là luật đắt nhất của Phase 8 và cũng là luật KHÔNG kiểm được bằng mắt trên giao diện:
/// muốn thấy một mục đi hết thang bằng tay thì phải chờ 24 ngày thật. Vì thế toàn bộ luật là
/// hàm thuần nhận <c>now</c>, và cả file này chạy dưới 20 ms.
/// </summary>
public class ReviewScheduleTests
{
    // Mốc cố định, có offset +07: nếu luật lỡ dùng DateTime thay vì DateTimeOffset thì phép
    // cộng ngày sẽ lệch đúng 7 tiếng và test này bắt được ngay.
    private static readonly DateTimeOffset Moc = new(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

    // ── Bậc đầu tiên ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Muc_moi_sinh_tu_cau_tra_loi_sai_co_han_on_sau_3_ngay()
    {
        var kq = ReviewSchedule.Start(Moc);

        kq.Stage.ShouldBe(ReviewStage.ThreeDays);
        kq.NextReviewAt.ShouldBe(Moc.AddDays(3));
        kq.IsMastered.ShouldBeFalse();
    }

    // ── Thang đủ bốn bậc ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Thang_on_tap_di_du_3_ngay_roi_1_tuan_roi_2_tuan_roi_thuoc()
    {
        var bac1 = ReviewSchedule.Start(Moc);
        bac1.NextReviewAt.ShouldBe(Moc.AddDays(3));

        var onLan1 = bac1.NextReviewAt!.Value;
        var bac2 = ReviewSchedule.Next(bac1.Stage, traLoiDung: true, onLan1);
        bac2.Stage.ShouldBe(ReviewStage.OneWeek);
        bac2.NextReviewAt.ShouldBe(onLan1.AddDays(7));

        var onLan2 = bac2.NextReviewAt!.Value;
        var bac3 = ReviewSchedule.Next(bac2.Stage, traLoiDung: true, onLan2);
        bac3.Stage.ShouldBe(ReviewStage.TwoWeeks);
        bac3.NextReviewAt.ShouldBe(onLan2.AddDays(14));

        var onLan3 = bac3.NextReviewAt!.Value;
        var bac4 = ReviewSchedule.Next(bac3.Stage, traLoiDung: true, onLan3);
        bac4.Stage.ShouldBe(ReviewStage.Mastered);
        bac4.IsMastered.ShouldBeTrue();

        // Thuộc rồi thì KHÔNG còn hạn ôn — đây là thứ giữ mục ra khỏi hàng đợi. Để lại một
        // ngày cũ ở đây là mục thuộc rồi vẫn hiện lên "Ôn tập hôm nay" mãi mãi.
        bac4.NextReviewAt.ShouldBeNull();
    }

    // ── Trả lời sai: quay về đầu chu kỳ ──────────────────────────────────────────────────

    [Theory]
    [InlineData(ReviewStage.ThreeDays)]
    [InlineData(ReviewStage.OneWeek)]
    [InlineData(ReviewStage.TwoWeeks)]
    public void Tra_loi_sai_o_BAT_KY_bac_nao_deu_quay_ve_3_ngay(ReviewStage bacHienTai)
    {
        var kq = ReviewSchedule.Next(bacHienTai, traLoiDung: false, Moc);

        kq.Stage.ShouldBe(ReviewStage.ThreeDays);
        kq.NextReviewAt.ShouldBe(Moc.AddDays(3));
    }

    [Fact]
    public void Tra_loi_sai_o_bac_CUOI_khong_thanh_thuoc_ma_ve_lai_3_ngay()
    {
        // Ca dễ viết sai nhất: bậc cuối cộng thêm một nấc là "thuộc", nên một nhánh switch
        // thiếu điều kiện đúng/sai sẽ cho sinh viên "thuộc" đúng lúc họ vừa trả lời sai.
        var kq = ReviewSchedule.Next(ReviewStage.TwoWeeks, traLoiDung: false, Moc);

        kq.Stage.ShouldBe(ReviewStage.ThreeDays);
        kq.IsMastered.ShouldBeFalse();
        kq.NextReviewAt.ShouldBe(Moc.AddDays(3));
    }

    [Fact]
    public void Sai_lai_mot_khai_niem_da_thuoc_thi_quay_ve_dau_chu_ky()
    {
        // Học môn sau mà sai lại khái niệm cũ là chuyện có thật, và đó chính là lý do
        // ReviewQueueItem gắn với user_id chứ không gắn với enrollment_id (Gap #2).
        var kq = ReviewSchedule.Next(ReviewStage.Mastered, traLoiDung: false, Moc);

        kq.Stage.ShouldBe(ReviewStage.ThreeDays);
        kq.NextReviewAt.ShouldBe(Moc.AddDays(3));
    }

    [Fact]
    public void Da_thuoc_ma_tra_loi_dung_them_lan_nua_van_la_thuoc()
    {
        var kq = ReviewSchedule.Next(ReviewStage.Mastered, traLoiDung: true, Moc);

        kq.Stage.ShouldBe(ReviewStage.Mastered);
        kq.NextReviewAt.ShouldBeNull();
    }

    // ── Lịch tính từ lúc TRẢ LỜI, không từ hạn cũ ────────────────────────────────────────

    [Fact]
    public void On_muon_thi_lich_ke_tiep_tinh_tu_luc_tra_loi_chu_khong_tu_han_cu()
    {
        // Sinh viên bỏ quên hàng đợi 10 ngày rồi mới ôn. Tính mốc kế tiếp từ hạn cũ
        // (Moc+3+7) là ép họ ôn lại chỉ 4 hôm sau — nén lịch đúng lúc họ đang học chậm.
        var onMuon = Moc.AddDays(13);

        var kq = ReviewSchedule.Next(ReviewStage.ThreeDays, traLoiDung: true, onMuon);

        kq.NextReviewAt.ShouldBe(onMuon.AddDays(7));
    }

    // ── Mục trong hàng đợi ───────────────────────────────────────────────────────────────

    [Fact]
    public void Muc_moi_gan_voi_NGUOI_HOC_va_dem_ngay_mot_lan_sai()
    {
        var nguoiHoc = Guid.CreateVersion7();
        var cauHoi = Guid.CreateVersion7();
        var luotLam = Guid.CreateVersion7();

        var muc = ReviewQueueItem.Create(nguoiHoc, cauHoi, luotLam, Moc);

        muc.UserId.ShouldBe(nguoiHoc);
        muc.QuestionId.ShouldBe(cauHoi);
        muc.SourceQuizAttemptId.ShouldBe(luotLam);
        muc.Stage.ShouldBe(ReviewStage.ThreeDays);
        muc.Status.ShouldBe(ReviewItemStatus.Pending);
        muc.NextReviewAt.ShouldBe(Moc.AddDays(3));

        // Mục tồn tại VÌ một câu trả lời sai — đếm từ 0 là làm mất chính sự kiện sinh ra nó.
        muc.WrongCount.ShouldBe(1);
        muc.CorrectCount.ShouldBe(0);
    }

    [Fact]
    public void Muc_chi_den_han_khi_da_qua_moc_next_review_at()
    {
        var muc = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Moc);

        muc.IsDue(Moc).ShouldBeFalse();
        muc.IsDue(Moc.AddDays(2).AddHours(23)).ShouldBeFalse();
        muc.IsDue(Moc.AddDays(3)).ShouldBeTrue();
        muc.IsDue(Moc.AddDays(30)).ShouldBeTrue();
    }

    [Fact]
    public void Muc_da_thuoc_khong_bao_gio_vao_hang_doi_nua()
    {
        var muc = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Moc);

        muc.ApplyAnswer(dung: true, Moc.AddDays(3));
        muc.ApplyAnswer(dung: true, Moc.AddDays(10));
        muc.ApplyAnswer(dung: true, Moc.AddDays(24));

        muc.Stage.ShouldBe(ReviewStage.Mastered);
        muc.Status.ShouldBe(ReviewItemStatus.Mastered);
        muc.NextReviewAt.ShouldBeNull();
        muc.CorrectCount.ShouldBe(3);

        // Dù đẩy thời gian đi bao xa cũng không đến hạn nữa.
        muc.IsDue(Moc.AddYears(5)).ShouldBeFalse();
    }

    [Fact]
    public void Tra_loi_sai_dua_muc_da_thuoc_tro_lai_trang_thai_cho_on()
    {
        var muc = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Moc);
        muc.ApplyAnswer(dung: true, Moc.AddDays(3));
        muc.ApplyAnswer(dung: true, Moc.AddDays(10));
        muc.ApplyAnswer(dung: true, Moc.AddDays(24));

        var saiLai = Moc.AddDays(100);
        muc.ApplyAnswer(dung: false, saiLai);

        muc.Stage.ShouldBe(ReviewStage.ThreeDays);
        muc.Status.ShouldBe(ReviewItemStatus.Pending);
        muc.NextReviewAt.ShouldBe(saiLai.AddDays(3));
        muc.WrongCount.ShouldBe(2);          // một lần lúc sinh ra, một lần vừa rồi
        muc.LastReviewedAt.ShouldBe(saiLai);
    }

    // ── Kịch bản nghiệm thu của plan, viết thành một test ────────────────────────────────

    [Fact]
    public void Nghiem_thu_sai_3_cau_thi_3_muc_vao_hang_doi_va_thang_chay_dung()
    {
        var nguoiHoc = Guid.CreateVersion7();
        var luotLam = Guid.CreateVersion7();
        var cauSai = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };

        var hangDoi = cauSai
            .Select(c => ReviewQueueItem.Create(nguoiHoc, c, luotLam, Moc))
            .ToList();

        // 3 mục vào hàng đợi với hạn ôn = +3 ngày.
        hangDoi.Count.ShouldBe(3);
        hangDoi.ShouldAllBe(m => m.NextReviewAt == Moc.AddDays(3));

        // Đẩy thời gian 3 ngày → cả 3 xuất hiện ở "Ôn tập hôm nay".
        var sauBaNgay = Moc.AddDays(3);
        hangDoi.Count(m => m.IsDue(sauBaNgay)).ShouldBe(3);

        // Trả lời ĐÚNG → lịch sau là 1 tuần.
        hangDoi[0].ApplyAnswer(dung: true, sauBaNgay);
        hangDoi[0].Stage.ShouldBe(ReviewStage.OneWeek);
        hangDoi[0].NextReviewAt.ShouldBe(sauBaNgay.AddDays(7));

        // Trả lời SAI → quay lại 3 ngày.
        hangDoi[1].ApplyAnswer(dung: false, sauBaNgay);
        hangDoi[1].Stage.ShouldBe(ReviewStage.ThreeDays);
        hangDoi[1].NextReviewAt.ShouldBe(sauBaNgay.AddDays(3));
    }
}
