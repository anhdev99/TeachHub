using Shouldly;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Tests;

public class LessonStructureTests
{
    private static Lesson BaiMoi(string title = "x") =>
        new() { Title = title, OrderIndex = 0, ContentType = LessonContentType.Text };

    [Fact]
    public void Mot_lesson_khong_co_component_nao_van_hop_le()
    {
        // Đúng ví dụ trong SRS mục 5.1: "chương có 2 bài, 1 bài có bài tập +
        // trắc nghiệm, bài kia không có gì".
        var lesson = new Lesson { Title = "Giới thiệu", OrderIndex = 0, ContentType = LessonContentType.Ppt };

        lesson.Components.ShouldBeEmpty();
        lesson.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void Mot_lesson_gan_duoc_nhieu_component_khac_loai()
    {
        var lesson = new Lesson { Title = "Hooks", OrderIndex = 0, ContentType = LessonContentType.Ppt };
        var assignmentId = Guid.CreateVersion7();

        lesson.AddComponent(LessonComponentType.Recap, refId: null, new ComponentConfig { RecapQuestionCount = 3 });
        lesson.AddComponent(LessonComponentType.Assignment, refId: assignmentId, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.Reflection, refId: null,
            new ComponentConfig { ReflectionPrompt = "3 điều em nhớ nhất?" });

        lesson.Components.Count.ShouldBe(3);
        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Component_loai_assignment_bat_buoc_co_ref_id()
    {
        Should.Throw<ArgumentException>(() =>
            BaiMoi().AddComponent(LessonComponentType.Assignment, refId: null, new ComponentConfig()));
    }

    [Fact]
    public void Component_loai_noi_dung_nhung_khong_duoc_co_ref_id()
    {
        Should.Throw<ArgumentException>(() =>
            BaiMoi().AddComponent(LessonComponentType.Reflection, refId: Guid.CreateVersion7(), new ComponentConfig()));
    }

    [Fact]
    public void Sap_xep_lai_component_giu_lien_tuc_order_index()
    {
        var lesson = BaiMoi();
        lesson.AddComponent(LessonComponentType.Hook, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.Reflection, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.CustomNote, null, new ComponentConfig());

        lesson.MoveComponent(lesson.Components[2].Id, toIndex: 0);

        lesson.Components.Select(c => c.Type)
            .ShouldBe([LessonComponentType.CustomNote, LessonComponentType.Hook, LessonComponentType.Reflection]);
        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Xoa_component_van_giu_order_index_lien_tuc()
    {
        var lesson = BaiMoi();
        lesson.AddComponent(LessonComponentType.Hook, null, new ComponentConfig());
        var giua = lesson.AddComponent(LessonComponentType.Reflection, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.CustomNote, null, new ComponentConfig());

        lesson.RemoveComponent(giua.Id);

        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1]);
    }

    [Fact]
    public void Chuong_sap_xep_lai_bai_van_giu_order_index_lien_tuc()
    {
        var chuong = new Chapter { SubjectVersionId = Guid.CreateVersion7(), Title = "Chương 1", OrderIndex = 0 };
        chuong.AddLesson(BaiMoi("A"));
        chuong.AddLesson(BaiMoi("B"));
        var c = chuong.AddLesson(BaiMoi("C"));

        chuong.MoveLesson(c.Id, toIndex: 0);

        chuong.Lessons.Select(l => l.Title).ShouldBe(["C", "A", "B"]);
        chuong.Lessons.Select(l => l.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Xoa_bai_khoi_chuong_van_giu_order_index_lien_tuc()
    {
        var chuong = new Chapter { SubjectVersionId = Guid.CreateVersion7(), Title = "Chương 1", OrderIndex = 0 };
        chuong.AddLesson(BaiMoi("A"));
        var giua = chuong.AddLesson(BaiMoi("B"));
        chuong.AddLesson(BaiMoi("C"));

        chuong.RemoveLesson(giua.Id);

        chuong.Lessons.Select(l => l.Title).ShouldBe(["A", "C"]);
        chuong.Lessons.Select(l => l.OrderIndex).ShouldBe([0, 1]);
    }

    /// <summary>
    /// EF nạp collection navigation KHÔNG kèm ORDER BY, nên thứ tự phần tử trong danh sách là
    /// thứ tự heap của Postgres — sau vài lần xóa/thêm nó không còn trùng OrderIndex. Domain
    /// phải tự sắp theo OrderIndex trước khi đánh số lại; tin vào thứ tự nạp là hoán vị các
    /// phần của bài học một cách ngẫu nhiên mà không có lỗi nào báo.
    /// </summary>
    [Fact]
    public void Xoa_component_khi_thu_tu_nap_lech_OrderIndex_van_giu_dung_thu_tu()
    {
        var bai = BaiMoi();

        // Dựng đúng cảnh EF trả về: danh sách xếp lộn xộn, OrderIndex mới là chân lý.
        var a = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.Hook, OrderIndex = 0 };
        var b = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.Reflection, OrderIndex = 1 };
        var c = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.CustomNote, OrderIndex = 2 };
        bai.Components.AddRange([c, a, b]);

        bai.RemoveComponent(b.Id);

        bai.Components.OrderBy(x => x.OrderIndex).Select(x => x.Type)
           .ShouldBe([LessonComponentType.Hook, LessonComponentType.CustomNote]);
        a.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
    }

    [Fact]
    public void Keo_tha_component_khi_thu_tu_nap_lech_OrderIndex_van_dat_dung_cho()
    {
        var bai = BaiMoi();
        var a = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.Hook, OrderIndex = 0 };
        var b = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.Reflection, OrderIndex = 1 };
        var c = new LessonComponent { LessonId = bai.Id, Type = LessonComponentType.CustomNote, OrderIndex = 2 };
        bai.Components.AddRange([c, a, b]);

        // Đưa phần ĐẦU (theo OrderIndex) xuống cuối. Thứ tự đúng là [b, c, a].
        bai.MoveComponent(a.Id, 2);

        b.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
        a.OrderIndex.ShouldBe(2);
    }

    [Fact]
    public void Xoa_bai_hoc_khi_thu_tu_nap_lech_OrderIndex_van_giu_dung_thu_tu()
    {
        var chuong = new Chapter { Title = "C1", OrderIndex = 0 };
        var a = new Lesson { ChapterId = chuong.Id, Title = "A", OrderIndex = 0, ContentType = LessonContentType.Text };
        var b = new Lesson { ChapterId = chuong.Id, Title = "B", OrderIndex = 1, ContentType = LessonContentType.Text };
        var c = new Lesson { ChapterId = chuong.Id, Title = "C", OrderIndex = 2, ContentType = LessonContentType.Text };
        chuong.Lessons.AddRange([c, a, b]);

        chuong.RemoveLesson(b.Id);

        chuong.Lessons.OrderBy(l => l.OrderIndex).Select(l => l.Title).ShouldBe(["A", "C"]);
        a.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
    }

    [Fact]
    public void ComponentConfig_deep_copy_khong_dung_chung_danh_sach_voi_ban_goc()
    {
        // Cùng lý do với Syllabus.DeepCopy: clone version mà chia sẻ danh sách là
        // sửa bản mới đổi luôn bản cũ.
        var goc = new ComponentConfig
        {
            RecapFoundationTags = ["hooks"],
            InlineQuestions = [new InlineQuestion { Text = "Câu 1", Options = ["A", "B"], CorrectIndexes = [0] }],
        };

        var ban = goc.DeepCopy();
        ban.RecapFoundationTags.Add("state");
        ban.InlineQuestions[0].Text = "Đã sửa";
        ban.InlineQuestions[0].Options.Add("C");

        goc.RecapFoundationTags.Count.ShouldBe(1);
        goc.InlineQuestions[0].Text.ShouldBe("Câu 1");
        goc.InlineQuestions[0].Options.Count.ShouldBe(2);
    }
}
