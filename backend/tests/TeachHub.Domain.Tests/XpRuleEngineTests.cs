using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Bảng quy tắc XP là DỮ LIỆU, không phải mã. Mọi con số trong bộ test này đều do người gọi
/// dựng ra — engine không được biết trước con số nào cả.
/// </summary>
public class XpRuleEngineTests
{
    private static XpRuleSpec Quy(XpEventType loai, int diem, decimal? nguong = null,
                                  int? tranNgay = null, bool bat = true) =>
        new() { EventType = loai, Points = diem, MinValue = nguong, DailyCap = tranNgay, IsEnabled = bat };

    [Fact]
    public void Su_kien_khong_co_quy_tac_nao_thi_khong_duoc_XP()
    {
        var kq = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 9m),
                                       [Quy(XpEventType.LessonCompleted, 10)]);

        kq.Points.ShouldBe(0);
        kq.IsAwarded.ShouldBeFalse();
        kq.Rule.ShouldBeNull();
    }

    [Fact]
    public void Quy_tac_bi_tat_khong_duoc_ap_dung()
    {
        // Tắt quy tắc phải có hiệu lực ngay giữa kỳ mà không cần xóa nó đi — xóa là mất luôn
        // lịch sử vì sao sinh viên từng được cộng.
        var kq = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted),
                                       [Quy(XpEventType.LessonCompleted, 10, bat: false)]);

        kq.Points.ShouldBe(0);
    }

    [Fact]
    public void Nhieu_bac_nguong_thi_bac_CAO_NHAT_thang_the_chu_khong_cong_don()
    {
        // Quiz 9.5 điểm phải nhận 30 XP của bậc "≥ 9", KHÔNG phải 50 (= 20 + 30) và cũng
        // không phải 20 của bậc thấp hơn. Cộng dồn là cách bảng quy tắc hai bậc âm thầm
        // biến thành một bảng ba bậc mà không ai khai.
        IReadOnlyList<XpRuleSpec> bang =
        [
            Quy(XpEventType.QuizHighScore, 20, nguong: 8m),
            Quy(XpEventType.QuizHighScore, 30, nguong: 9m),
        ];

        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 9.5m), bang).Points.ShouldBe(30);
        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 8.5m), bang).Points.ShouldBe(20);
    }

    [Fact]
    public void Diem_dung_bang_nguong_van_duoc_tinh()
    {
        // Ngưỡng là "từ … trở lên". Dùng > thay vì >= ở đây thì sinh viên được đúng 8.0 điểm
        // không có XP, và giải thích với họ là chuyện không thể.
        var kq = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 8m),
                                       [Quy(XpEventType.QuizHighScore, 20, nguong: 8m)]);

        kq.Points.ShouldBe(20);
    }

    [Fact]
    public void Duoi_nguong_thi_khong_quy_tac_nao_ap_dung()
    {
        var kq = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore, 7.9m),
                                       [Quy(XpEventType.QuizHighScore, 20, nguong: 8m)]);

        kq.Points.ShouldBe(0);
    }

    [Fact]
    public void Su_kien_khong_kem_gia_tri_khong_kich_hoat_quy_tac_co_nguong()
    {
        // Không có giá trị mà vẫn cho qua ngưỡng là mở đường cho mọi sự kiện lạ ăn XP bậc cao.
        var kq = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.QuizHighScore),
                                       [Quy(XpEventType.QuizHighScore, 20, nguong: 8m)]);

        kq.Points.ShouldBe(0);
    }

    [Fact]
    public void Tran_moi_ngay_chan_lan_vuot_qua()
    {
        var bang = new[] { Quy(XpEventType.LessonCompleted, 10, tranNgay: 3) };

        XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 2), bang)
            .Points.ShouldBe(10);

        var chan = XpRuleEngine.Evaluate(
            new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 3), bang);

        chan.Points.ShouldBe(0);
        chan.SkipReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Khong_khai_tran_thi_khong_gioi_han_so_lan_trong_ngay()
    {
        var kq = XpRuleEngine.Evaluate(
            new XpEventInput(XpEventType.LessonCompleted, AwardedTodayForType: 99),
            [Quy(XpEventType.LessonCompleted, 10)]);

        kq.Points.ShouldBe(10);
    }

    [Fact]
    public void Bang_quy_tac_khoi_tao_phu_du_ba_su_kien_cua_de_bai()
    {
        // SeedRules chỉ là bộ GIEO cho một lớp mới — vẫn là dữ liệu, sửa được sau đó.
        // Nó tồn tại để ngày đầu tiên mở lớp không phải là một bảng trống.
        var bang = XpRuleEngine.SeedRules();

        bang.ShouldContain(r => r.EventType == XpEventType.LessonCompleted);
        bang.ShouldContain(r => r.EventType == XpEventType.AssignmentSubmittedOnTime);
        bang.ShouldContain(r => r.EventType == XpEventType.QuizHighScore);
        bang.ShouldAllBe(r => r.Points > 0);
    }

    [Fact]
    public void Hai_quy_tac_trung_nguong_van_cho_ket_qua_TAT_DINH()
    {
        // Dữ liệu trùng lặp là chuyện sớm muộn (giảng viên bấm lưu hai lần). Kết quả phải
        // không phụ thuộc thứ tự phần tử, nếu không cùng một sinh viên đọc hai lần ra hai số.
        IReadOnlyList<XpRuleSpec> xuoi = [Quy(XpEventType.LessonCompleted, 5), Quy(XpEventType.LessonCompleted, 12)];
        IReadOnlyList<XpRuleSpec> nguoc = [Quy(XpEventType.LessonCompleted, 12), Quy(XpEventType.LessonCompleted, 5)];

        var a = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted), xuoi).Points;
        var b = XpRuleEngine.Evaluate(new XpEventInput(XpEventType.LessonCompleted), nguoc).Points;

        a.ShouldBe(b);
        a.ShouldBe(12);
    }
}
