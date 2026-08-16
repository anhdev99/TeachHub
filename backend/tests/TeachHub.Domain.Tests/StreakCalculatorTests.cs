using Shouldly;
using TeachHub.Domain.Gamification;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Streak là chỗ dễ sai nhất của toàn bộ cơ chế động lực học tập, và là chỗ sinh viên phát hiện
/// nhanh nhất: một người học đều 5 ngày mà thấy con số 3 sẽ mất niềm tin vào mọi thứ còn lại.
/// Cả bộ test này chỉ để trả lời đúng MỘT câu hỏi: "ngày" là ngày nào.
/// </summary>
public class StreakCalculatorTests
{
    private static readonly TimeZoneInfo Vn = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly TimeSpan LechVn = TimeSpan.FromHours(7);

    /// <summary>Một mốc thời gian viết theo GIỜ VIỆT NAM cho dễ đọc.</summary>
    private static DateTimeOffset GioVn(int ngay, int gio, int phut = 0) =>
        new(2026, 8, ngay, gio, phut, 0, LechVn);

    [Fact]
    public void Hoc_luc_23h50_roi_00h10_hom_sau_tinh_thanh_HAI_ngay()
    {
        // Ca kinh điển: hai mốc chỉ cách nhau 20 phút nhưng nằm ở hai NGÀY ĐỊA PHƯƠNG khác nhau.
        // Sinh viên học khuya rồi học tiếp sau nửa đêm đã giữ được chuỗi hai ngày — trả về 1 là
        // ăn cắp một ngày của họ, và họ đếm được.
        var toiKhuya = GioVn(16, 23, 50);
        var quaNuaDem = GioVn(17, 0, 10);

        var kq = StreakCalculator.Compute([toiKhuya, quaNuaDem], Vn, now: quaNuaDem);

        kq.Current.ShouldBe(2);
        kq.Longest.ShouldBe(2);
    }

    [Fact]
    public void Cung_hai_moc_do_nhung_tinh_theo_UTC_chi_ra_MOT_ngay()
    {
        // 23:50 giờ VN = 16:50Z, 00:10 hôm sau giờ VN = 17:10Z — CÙNG một ngày UTC.
        // Đây chính là lý do phải quy đổi qua múi giờ của lớp trước khi so sánh: chọn nhầm
        // múi giờ không làm sai lệch một chút, nó làm mất hẳn một ngày trong chuỗi.
        var kq = StreakCalculator.Compute(
            [GioVn(16, 23, 50), GioVn(17, 0, 10)], TimeZoneInfo.Utc, now: GioVn(17, 0, 10));

        kq.Current.ShouldBe(1);
        kq.Longest.ShouldBe(1);
    }

    [Fact]
    public void Ba_ngay_lien_tiep_cho_streak_bang_ba()
    {
        var kq = StreakCalculator.Compute(
            [GioVn(14, 9), GioVn(15, 20), GioVn(16, 8)], Vn, now: GioVn(16, 22));

        kq.Current.ShouldBe(3);
        kq.Longest.ShouldBe(3);
    }

    [Fact]
    public void Bo_mot_ngay_thi_streak_ve_mot()
    {
        // Học ngày 12, 13, 14 — nghỉ ngày 15 — học lại ngày 16.
        var kq = StreakCalculator.Compute(
            [GioVn(12, 9), GioVn(13, 9), GioVn(14, 9), GioVn(16, 9)], Vn, now: GioVn(16, 21));

        kq.Current.ShouldBe(1);

        // Kỷ lục cũ KHÔNG bị xóa khi chuỗi đứt: huy hiệu đã trao dựa trên kỷ lục thì không
        // được phép biến mất, nếu không sinh viên mất huy hiệu vì một ngày ốm.
        kq.Longest.ShouldBe(3);
    }

    [Fact]
    public void Hai_hoat_dong_trong_cung_mot_ngay_dia_phuong_chi_tinh_mot_ngay()
    {
        // Học chăm gấp đôi trong một ngày không mua được thêm ngày streak.
        var kq = StreakCalculator.Compute(
            [GioVn(16, 7, 30), GioVn(16, 23, 30)], Vn, now: GioVn(16, 23, 40));

        kq.Current.ShouldBe(1);
        kq.Longest.ShouldBe(1);
    }

    [Fact]
    public void Chua_hoc_hom_nay_nhung_hom_qua_co_thi_chuoi_van_con_song()
    {
        // Không có ân hạn tới hết ngày thì mọi sinh viên mở trang lúc 7 giờ sáng đều thấy
        // streak = 0 và tưởng mình vừa mất chuỗi — rồi không ai tin con số đó nữa.
        var kq = StreakCalculator.Compute(
            [GioVn(14, 9), GioVn(15, 9), GioVn(16, 9)], Vn, now: GioVn(17, 7));

        kq.Current.ShouldBe(3);
    }

    [Fact]
    public void Hoat_dong_gan_nhat_cach_hai_ngay_thi_streak_ve_khong()
    {
        var kq = StreakCalculator.Compute([GioVn(14, 9)], Vn, now: GioVn(16, 9));

        kq.Current.ShouldBe(0);
        kq.Longest.ShouldBe(1);
    }

    [Fact]
    public void Khong_co_hoat_dong_nao_thi_streak_bang_khong()
    {
        var kq = StreakCalculator.Compute([], Vn, now: GioVn(16, 9));

        kq.Current.ShouldBe(0);
        kq.Longest.ShouldBe(0);
        kq.LastActiveLocalDate.ShouldBeNull();
    }

    [Fact]
    public void Moc_thoi_gian_khong_can_sap_xep_san()
    {
        // Truy vấn trả về theo thứ tự nào là chuyện của câu truy vấn; kết quả streak không
        // được phép phụ thuộc vào nó.
        var kq = StreakCalculator.Compute(
            [GioVn(16, 9), GioVn(14, 9), GioVn(15, 9)], Vn, now: GioVn(16, 10));

        kq.Current.ShouldBe(3);
        kq.LastActiveLocalDate.ShouldBe(new DateOnly(2026, 8, 16));
    }

    [Fact]
    public void Mui_gio_khong_hop_le_lui_ve_gio_Viet_Nam_chu_khong_nem_loi()
    {
        // TimeZoneId là chuỗi tự do trong DB. Ném ngoại lệ ở đây là để một dòng dữ liệu hỏng
        // làm sập trang tiến độ của cả lớp; lùi về mặc định thì sai lệch nhiều nhất là vài giờ.
        StreakCalculator.ResolveZone("Khong/Ton_Tai").ShouldBe(Vn);
        StreakCalculator.ResolveZone(null).ShouldBe(Vn);
        StreakCalculator.ResolveZone("  ").ShouldBe(Vn);
        StreakCalculator.ResolveZone("Asia/Tokyo").Id.ShouldBe("Asia/Tokyo");
    }

    [Fact]
    public void Ngay_dia_phuong_doi_theo_mui_gio_cua_lop()
    {
        var moc = new DateTimeOffset(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);   // 17:30Z

        StreakCalculator.LocalDateOf(moc, Vn).ShouldBe(new DateOnly(2026, 8, 17));       // 00:30 hôm sau
        StreakCalculator.LocalDateOf(moc, TimeZoneInfo.Utc).ShouldBe(new DateOnly(2026, 8, 16));
    }
}
