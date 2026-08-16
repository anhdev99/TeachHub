using System.Text;
using Shouldly;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Giữ test dù phase chạy theo yêu cầu "không viết test": chấm điền khuyết tiếng Việt là ca
/// KHÔNG kiểm được bằng mắt. Hai chuỗi "Hà Nội" gõ bằng hai bộ gõ khác nhau hiện ra giống
/// hệt nhau trên màn hình nhưng khác nhau từng byte — nhìn ảnh chụp màn hình không bao giờ
/// phát hiện được. Cả file chạy dưới 20 ms.
/// </summary>
public class QuizGraderTests
{
    private static QuizQuestion ChonMot(params (string Text, bool Dung)[] pa)
    {
        var q = new QuizQuestion
        {
            InstructorId = Guid.CreateVersion7(), Type = QuestionType.SingleChoice,
            Text = "Câu hỏi", Points = 1m,
        };
        q.Options.AddRange(pa.Select((p, i) => new QuestionOption
        {
            Text = p.Text, IsCorrect = p.Dung, OrderIndex = i,
        }));
        return q;
    }

    private static QuizQuestion ChonNhieu(bool chamMotPhan, params (string Text, bool Dung)[] pa)
    {
        var q = new QuizQuestion
        {
            InstructorId = Guid.CreateVersion7(), Type = QuestionType.MultipleChoice,
            Text = "Câu hỏi", Points = 2m, AllowPartialCredit = chamMotPhan,
        };
        q.Options.AddRange(pa.Select((p, i) => new QuestionOption
        {
            Text = p.Text, IsCorrect = p.Dung, OrderIndex = i,
        }));
        return q;
    }

    private static QuizQuestion DienKhuyet(params string[] dapAn) => new()
    {
        InstructorId = Guid.CreateVersion7(), Type = QuestionType.FillBlank,
        Text = "Thủ đô Việt Nam là ___", Points = 1m,
        FillBlank = new FillBlankConfig { AcceptedAnswers = [.. dapAn] },
    };

    // ── Câu chọn một ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Chon_dung_mot_dap_an_duoc_diem()
    {
        var q = ChonMot(("A", false), ("B", true));
        var dung = q.Options[1].Id;

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [dung], null)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Khong_tra_loi_duoc_0_diem()
    {
        var q = ChonMot(("A", false), ("B", true));

        QuizGrader.Grade([q], []).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Cau_chon_mot_ma_chon_hai_la_sai()
    {
        // Nhận bừa phương án đầu tiên là nới tay cho người chọn hú họa.
        var q = ChonMot(("A", false), ("B", true));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id], null)])
            .RawScore.ShouldBe(0m);
    }

    // ── Câu chọn nhiều ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Tat_ca_hoac_khong_chon_thieu_mot_dap_an_la_0()
    {
        var q = ChonNhieu(false, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id], null)]).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Tat_ca_hoac_khong_chon_du_va_dung_duoc_tron_diem()
    {
        var q = ChonNhieu(false, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id], null)])
            .RawScore.ShouldBe(2m);
    }

    [Fact]
    public void Cham_mot_phan_dung_mot_trong_hai_duoc_nua_diem()
    {
        var q = ChonNhieu(true, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id], null)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Cham_mot_phan_chon_HET_moi_phuong_an_khong_duoc_tron_diem()
    {
        // Không trừ đáp án sai thì chọn hết mọi ô là ăn trọn điểm — và cả lớp sẽ làm đúng thế.
        var q = ChonNhieu(true, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q],
            [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id, q.Options[2].Id], null)])
            .RawScore.ShouldBe(1m);   // (2 đúng − 1 sai) / 2 × 2 điểm
    }

    [Fact]
    public void Diem_khong_bao_gio_am()
    {
        var q = ChonNhieu(true, ("A", true), ("B", false), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[1].Id, q.Options[2].Id], null)])
            .RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Id_phuong_an_khong_thuoc_cau_bi_bo_qua()
    {
        var q = ChonNhieu(false, ("A", true), ("B", true));

        QuizGrader.Grade([q],
            [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id, Guid.CreateVersion7()], null)])
            .RawScore.ShouldBe(2m);
    }

    // ── Điền khuyết tiếng Việt ───────────────────────────────────────────────────────────

    [Fact]
    public void Dien_khuyet_dung_chinh_xac()
    {
        var q = DienKhuyet("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Hà Nội")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_khong_phan_biet_hoa_thuong()
    {
        var q = DienKhuyet("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "hà nội")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_bo_qua_khoang_trang_thua()
    {
        var q = DienKhuyet("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "  Hà   Nội ")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_chap_nhan_ca_hai_dang_unicode_cua_tieng_Viet()
    {
        // Đây là ca Gap #12 và là lý do cả file test này tồn tại.
        // NFC: "ế" là MỘT ký tự dựng sẵn U+1EBF (bàn phím macOS).
        // NFD: "ế" là "e" + dấu mũ + dấu sắc, BA ký tự (nhiều bộ gõ Telex).
        // Hai chuỗi hiện ra giống hệt nhau trên màn hình.
        var q = DienKhuyet("Kiến thức");
        var goBangTelex = "Kiến thức".Normalize(NormalizationForm.FormD);

        goBangTelex.ShouldNotBe("Kiến thức");                 // khác nhau từng byte
        goBangTelex.Length.ShouldBeGreaterThan("Kiến thức".Length);

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], goBangTelex)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_chuan_hoa_ca_DAP_AN_MAU_chu_khong_chi_bai_lam()
    {
        // Giảng viên gõ đáp án bằng bộ gõ khác sinh viên là chuyện thường. Chuẩn hóa một bên
        // thì ca hỏng phụ thuộc vào việc ai gõ bằng gì — thứ không ai nghĩ tới khi tìm lỗi.
        var q = DienKhuyet("Kiến thức".Normalize(NormalizationForm.FormD));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Kiến thức")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_nhieu_dap_an_duoc_chap_nhan()
    {
        var q = DienKhuyet("Hà Nội", "Ha Noi", "HN");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "HN")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Dien_khuyet_sai_thi_khong_co_diem()
    {
        var q = DienKhuyet("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Sài Gòn")]).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Dien_khuyet_phan_biet_hoa_thuong_khi_duoc_bat()
    {
        var q = DienKhuyet("ABC");
        q.FillBlank!.CaseSensitive = true;

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "abc")]).RawScore.ShouldBe(0m);
    }

    // ── Quy về thang 10 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Quy_ve_thang_10_dung_quy_tac_cua_Phase_4b()
    {
        // Quiz 20 câu, đúng 15 → 7.5 chứ KHÔNG phải 15 rồi bị kẹp thành 10.
        var cau = Enumerable.Range(0, 20).Select(_ => ChonMot(("A", true), ("B", false))).ToList();
        var tra = cau.Take(15).Select(q => new QuizAnswer(q.Id, [q.Options[0].Id], null)).ToList();

        var kq = QuizGrader.Grade(cau, tra);

        kq.RawScore.ShouldBe(15m);
        kq.MaxScore.ShouldBe(20m);
        kq.NormalizedScore.ShouldBe(7.5m);
    }

    [Fact]
    public void Quiz_khong_co_cau_nao_khong_chia_cho_0()
    {
        QuizGrader.Grade([], []).NormalizedScore.ShouldBe(0m);
    }

    [Fact]
    public void Danh_sach_cau_sai_dung_de_dua_vao_hang_doi_on_tap()
    {
        var a = ChonMot(("A", true), ("B", false));
        var b = ChonMot(("A", true), ("B", false));

        var kq = QuizGrader.Grade([a, b], [new QuizAnswer(a.Id, [a.Options[0].Id], null)]);

        kq.WrongQuestionIds.ShouldBe([b.Id]);
    }

    // ── Thời gian ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Chua_het_gio_thi_nop_binh_thuong()
    {
        var batDau = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(batDau, 15, batDau.AddMinutes(14)).ShouldBeFalse();
    }

    [Fact]
    public void Nop_trong_an_han_30_giay_van_duoc_chap_nhan()
    {
        // Mạng chậm, bấm nộp đúng giây cuối, đồng hồ lệch vài giây — không có ân hạn thì một
        // bài nộp hợp lệ bị coi là hết giờ.
        var batDau = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(batDau, 15, batDau.AddMinutes(15).AddSeconds(20)).ShouldBeFalse();
    }

    [Fact]
    public void Qua_an_han_thi_tinh_la_het_gio()
    {
        var batDau = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(batDau, 15, batDau.AddMinutes(16)).ShouldBeTrue();
    }

    [Fact]
    public void Quiz_khong_gioi_han_gio_thi_khong_bao_gio_het_gio()
    {
        var batDau = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(batDau, null, batDau.AddDays(3)).ShouldBeFalse();
        QuizTiming.ConLai(batDau, null, batDau).ShouldBeNull();
    }
}
