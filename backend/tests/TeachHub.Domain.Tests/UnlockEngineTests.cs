using Shouldly;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Domain.Tests;

public class UnlockEngineTests
{
    private static LessonNode Bai(string ten, bool batBuoc, params ComponentNode[] components) =>
        new(Guid.CreateVersion7(), ten, batBuoc, [.. components]);

    private static ComponentNode Phan(bool batBuoc) => new(Guid.CreateVersion7(), batBuoc);

    [Fact]
    public void Chuong_free_mo_khoa_toan_bo_bai_ngay_tu_dau()
    {
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free,
            [Bai("Bài 1", batBuoc: true), Bai("Bài 2", batBuoc: true)]);

        var kq = UnlockEngine.Evaluate(chuong, Progress.Empty);

        kq.Values.ShouldAllBe(v => v == LessonAccess.Unlocked);
    }

    [Fact]
    public void Chuong_tuan_tu_chi_mo_bai_dau_tien_khi_chua_hoc_gi()
    {
        var b1 = Bai("Bài 1", batBuoc: true);
        var b2 = Bai("Bài 2", batBuoc: true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [b1, b2]);

        var kq = UnlockEngine.Evaluate(chuong, Progress.Empty);

        kq[b1.Id].ShouldBe(LessonAccess.Unlocked);
        kq[b2.Id].ShouldBe(LessonAccess.Locked);
    }

    [Fact]
    public void Hoan_thanh_bai_bat_buoc_thi_mo_bai_ke_tiep()
    {
        var b1 = Bai("Bài 1", batBuoc: true);
        var b2 = Bai("Bài 2", batBuoc: true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [b1, b2]);

        var kq = UnlockEngine.Evaluate(chuong, Progress.With(completedLessons: [b1.Id]));

        kq[b2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void Bai_KHONG_bat_buoc_khong_chan_bai_ke_tiep()
    {
        // is_required_to_unlock_next = false ⇒ bài tham khảo, bỏ qua được.
        var b1 = Bai("Bài đọc thêm", batBuoc: false);
        var b2 = Bai("Bài 2", batBuoc: true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [b1, b2]);

        var kq = UnlockEngine.Evaluate(chuong, Progress.Empty);

        kq[b2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void Bai_chi_duoc_tinh_hoan_thanh_khi_moi_component_bat_buoc_da_xong()
    {
        var phanBatBuoc = Phan(batBuoc: true);
        var phanTuyChon = Phan(batBuoc: false);
        var b1 = Bai("Bài 1", batBuoc: true, phanBatBuoc, phanTuyChon);
        var b2 = Bai("Bài 2", batBuoc: true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [b1, b2]);

        // Xem xong nội dung nhưng chưa nộp bài tập bắt buộc
        var chuaXong = Progress.With(completedLessons: [b1.Id], completedComponents: []);
        UnlockEngine.Evaluate(chuong, chuaXong)[b2.Id].ShouldBe(LessonAccess.Locked);

        var daXong = Progress.With(completedLessons: [b1.Id], completedComponents: [phanBatBuoc.Id]);
        UnlockEngine.Evaluate(chuong, daXong)[b2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void Bai_bi_khoa_neu_ra_duoc_ly_do_la_bai_nao_dang_chan()
    {
        // Chỉ hiện ổ khóa mà không nói vì sao là nguồn tin nhắn hỏi bài số một của mọi LMS.
        var b1 = Bai("Bài 1 — Hooks", batBuoc: true);
        var b2 = Bai("Bài 2", batBuoc: true);
        var b3 = Bai("Bài 3", batBuoc: true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [b1, b2, b3]);

        UnlockEngine.BlockingLesson(chuong, b3.Id, Progress.Empty)?.Title.ShouldBe("Bài 1 — Hooks");
        UnlockEngine.BlockingLesson(chuong, b1.Id, Progress.Empty).ShouldBeNull();
    }

    [Fact]
    public void Phan_tram_hoan_thanh_tinh_theo_so_bai_da_xong()
    {
        var b1 = Bai("1", true); var b2 = Bai("2", true); var b3 = Bai("3", true); var b4 = Bai("4", true);
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free, [b1, b2, b3, b4]);

        UnlockEngine.CompletionPercent(chuong, Progress.With(completedLessons: [b1.Id, b2.Id]))
            .ShouldBe(50m);
    }

    [Fact]
    public void Chuong_rong_khong_gay_chia_cho_khong()
    {
        var chuong = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free, []);
        UnlockEngine.CompletionPercent(chuong, Progress.Empty).ShouldBe(0m);
    }
}
