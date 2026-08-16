using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

/// <summary>
/// Chấm câu hỏi nhúng của phần <c>checkpoint_quiz</c>. Trước khi có endpoint này, giao diện sinh
/// viên hiện được câu hỏi nhưng phải nói thẳng "lựa chọn của bạn chưa được chấm" — tức là một
/// màn hình có nút bấm mà không có hồi đáp.
/// </summary>
[Collection(TeachHubCollection.Ten)]
public class InlineCheckpointQuizTests(TeachHubApiFactory factory)
{
    /// <summary>
    /// Một bài học có đúng một phần checkpoint_quiz gồm 2 câu: câu 0 chọn-một (đáp án 1),
    /// câu 1 chọn-NHIỀU (đáp án 0 và 2). Câu chọn-nhiều là lý do Việc 2 tồn tại: không có cờ
    /// phân biệt thì giao diện render radio cho cả hai và câu 1 không bao giờ trả lời đúng được.
    /// </summary>
    private async Task<(HttpClient Sv, Guid OfferingId, Guid ComponentId)> SeedAsync(bool required = false)
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", unlock_rule = "free" });
        ch.EnsureSuccessStatusCode();
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b1 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "Bài 1" });
        b1.EnsureSuccessStatusCode();
        var lessonId = (await b1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cp = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new
        {
            type = "checkpoint_quiz",
            is_required = required,
            config = new
            {
                inline_questions = new object[]
                {
                    new
                    {
                        text = "Hook nào chạy sau mỗi lần render?",
                        options = new[] { "useState", "useEffect", "useMemo" },
                        correct_indexes = new[] { 1 },
                        explanation = "useEffect chạy sau khi DOM đã cập nhật.",
                    },
                    new
                    {
                        text = "Những hook nào trả về giá trị được ghi nhớ?",
                        options = new[] { "useMemo", "useEffect", "useCallback" },
                        correct_indexes = new[] { 0, 2 },
                        explanation = "useMemo ghi nhớ giá trị, useCallback ghi nhớ hàm.",
                    },
                },
            },
        });
        cp.EnsureSuccessStatusCode();
        var componentId = (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);
        var sv = await factory.LoginAsNewStudentOfAsync(offeringId);

        return (sv, offeringId, componentId);
    }

    [Fact]
    public async Task Cham_o_may_chu_va_tra_ve_dap_an_dung_kem_loi_giai()
    {
        var (sv, _, componentId) = await SeedAsync();

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[]
            {
                new { index = 0, selected_indexes = new[] { 1 } },   // đúng
                new { index = 1, selected_indexes = new[] { 0 } },   // thiếu một đáp án ⇒ sai
            },
        });
        res.EnsureSuccessStatusCode();

        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        kq.GetProperty("total").GetInt32().ShouldBe(2);
        kq.GetProperty("correct_count").GetInt32().ShouldBe(1);
        kq.GetProperty("all_correct").GetBoolean().ShouldBeFalse();

        var cau = kq.GetProperty("results").EnumerateArray().ToList();
        cau[0].GetProperty("is_correct").GetBoolean().ShouldBeTrue();
        cau[1].GetProperty("is_correct").GetBoolean().ShouldBeFalse();

        // Đáp án đúng và lời giải chỉ xuất hiện Ở ĐÂY — sau khi trả lời.
        cau[1].GetProperty("correct_indexes").EnumerateArray().Select(x => x.GetInt32())
              .ShouldBe([0, 2]);
        cau[1].GetProperty("explanation").GetString()!.ShouldContain("useCallback");
    }

    [Fact]
    public async Task Cau_chon_nhieu_tra_loi_du_thi_dung()
    {
        var (sv, _, componentId) = await SeedAsync();

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[]
            {
                new { index = 0, selected_indexes = new[] { 1 } },
                new { index = 1, selected_indexes = new[] { 2, 0 } },   // thứ tự không quan trọng
            },
        });
        res.EnsureSuccessStatusCode();

        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        kq.GetProperty("all_correct").GetBoolean().ShouldBeTrue();
        kq.GetProperty("correct_count").GetInt32().ShouldBe(2);
    }

    /// <summary>
    /// Việc 2. Không có cờ này thì giao diện render radio cho MỌI câu, và câu nhiều đáp án bị
    /// chấm sai một cách im lặng — sinh viên không có cách nào trả lời đúng nó.
    /// </summary>
    [Fact]
    public async Task Payload_bai_hoc_noi_ro_cau_nao_chon_mot_cau_nao_chon_nhieu()
    {
        var (sv, offeringId, componentId) = await SeedAsync();

        var tienDo = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        var phan = tienDo.GetProperty("chapters")[0].GetProperty("lessons")[0]
                         .GetProperty("components").EnumerateArray()
                         .Single(c => c.GetProperty("id").GetGuid() == componentId);

        var cau = phan.GetProperty("inline_questions").EnumerateArray().ToList();
        cau.Count.ShouldBe(2);
        cau[0].GetProperty("multi_select").GetBoolean().ShouldBeFalse();
        cau[1].GetProperty("multi_select").GetBoolean().ShouldBeTrue();

        // Index là số do MÁY CHỦ đánh, và chính nó là thứ được gửi lại lúc chấm.
        cau[0].GetProperty("index").GetInt32().ShouldBe(0);
        cau[1].GetProperty("index").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Payload_bai_hoc_KHONG_mang_dap_an_dung_hay_loi_giai()
    {
        // Gửi đáp án xuống rồi ẩn trên giao diện thì cả lớp mở DevTools ra đọc. Kiểm trên
        // CHUỖI JSON thô chứ không qua DTO: DTO chỉ nói được về những trường mình biết tên.
        var (sv, offeringId, _) = await SeedAsync();

        var json = await sv.GetStringAsync($"/api/offerings/{offeringId}/my-progress");

        json.ShouldNotContain("correct_indexes");
        json.ShouldNotContain("explanation");
        json.ShouldNotContain("useCallback ghi nhớ hàm");
    }

    [Fact]
    public async Task Tra_loi_dung_het_thi_phan_hoc_duoc_danh_dau_hoan_thanh()
    {
        var (sv, offeringId, componentId) = await SeedAsync(required: true);

        var truoc = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        truoc.GetProperty("chapters")[0].GetProperty("lessons")[0]
             .GetProperty("required_components_remaining").GetInt32().ShouldBe(1);

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[]
            {
                new { index = 0, selected_indexes = new[] { 1 } },
                new { index = 1, selected_indexes = new[] { 0, 2 } },
            },
        });
        res.EnsureSuccessStatusCode();
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completed").GetBoolean()
            .ShouldBeTrue();

        var sau = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        sau.GetProperty("chapters")[0].GetProperty("lessons")[0]
           .GetProperty("required_components_remaining").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Tra_loi_sai_thi_KHONG_danh_dau_hoan_thanh()
    {
        var (sv, offeringId, componentId) = await SeedAsync(required: true);

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[] { new { index = 0, selected_indexes = new[] { 0 } } },
        });
        res.EnsureSuccessStatusCode();
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completed").GetBoolean()
            .ShouldBeFalse();

        var sau = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        sau.GetProperty("chapters")[0].GetProperty("lessons")[0]
           .GetProperty("required_components_remaining").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Sinh_vien_lop_khac_cham_bai_cua_lop_nay_thi_404()
    {
        var (_, _, componentId) = await SeedAsync();
        var (svKhac, _, _) = await SeedAsync();   // lớp hoàn toàn khác

        var res = await svKhac.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[] { new { index = 0, selected_indexes = new[] { 1 } } },
        });

        // 404, KHÔNG BAO GIỜ 403: 403 xác nhận id đó có thật.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cau_hoi_khong_ton_tai_thi_400_chu_khong_phai_500()
    {
        var (sv, _, componentId) = await SeedAsync();

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[] { new { index = 7, selected_indexes = new[] { 0 } } },
        });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Phan_hoc_khong_co_cau_hoi_nhung_thi_400()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", unlock_rule = "free" });
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var b1 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "Bài 1" });
        var lessonId = (await b1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cp = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components",
            new { type = "reflection", is_required = false });
        cp.EnsureSuccessStatusCode();
        var componentId = (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);
        var sv = await factory.LoginAsNewStudentOfAsync(offeringId);

        var res = await sv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[] { new { index = 0, selected_indexes = new[] { 0 } } },
        });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Giang_vien_khong_goi_duoc_endpoint_cua_sinh_vien()
    {
        var (_, _, componentId) = await SeedAsync();
        var gv = await factory.LoginAsInstructorAsync();

        var res = await gv.PostAsJsonAsync($"/api/lesson-components/{componentId}/check", new
        {
            answers = new object[] { new { index = 0, selected_indexes = new[] { 1 } } },
        });

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
