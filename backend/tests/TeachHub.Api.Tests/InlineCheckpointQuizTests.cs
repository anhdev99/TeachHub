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

    /// <summary>
    /// Có ĐỐI CHỨNG DƯƠNG trong cùng một test: 404 một mình cũng là thứ một route gõ sai trả về,
    /// nên nếu chỉ khẳng định "người ngoài nhận 404" thì test vẫn xanh sau khi đường dẫn bị đổi
    /// nhầm — và phép kiểm quyền coi như hết được canh. Cú 200 của chính chủ trên CÙNG một URL
    /// là thứ nói rằng 404 phía trên đến từ việc không học lớp này, không đến từ định tuyến.
    /// </summary>
    [Fact]
    public async Task Sinh_vien_lop_khac_cham_bai_cua_lop_nay_thi_404()
    {
        var (sv, _, componentId) = await SeedAsync();
        var (svKhac, _, _) = await SeedAsync();   // lớp hoàn toàn khác
        var duongDan = $"/api/lesson-components/{componentId}/check";
        var than = new { answers = new object[] { new { index = 0, selected_indexes = new[] { 1 } } } };

        // 404, KHÔNG BAO GIỜ 403: 403 xác nhận id đó có thật.
        (await svKhac.PostAsJsonAsync(duongDan, than)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Chính chủ, cùng URL, cùng thân request: phải chấm được.
        (await sv.PostAsJsonAsync(duongDan, than)).StatusCode.ShouldBe(HttpStatusCode.OK);
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

    /// <summary>
    /// Bài học chưa mở khoá thì KHÔNG được gửi nội dung xuống, và cũng không chấm được.
    ///
    /// Trước đây `my-progress` trả về đầy đủ `html`, `file_url` và `inline_questions` bất kể
    /// `access == locked` — ổ khoá chỉ tồn tại ở giao diện, còn gọi thẳng API là đọc hết bài
    /// chưa mở, nên toàn bộ `UnlockEngine` chỉ là trang trí. Test này canh CẢ HAI đầu: đường
    /// đọc và đường chấm. Bịt một đầu mà để hở đầu kia thì cái khoá không tồn tại.
    /// </summary>
    [Fact]
    public async Task Bai_dang_khoa_KHONG_gui_noi_dung_va_KHONG_cham_duoc()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        // `after_previous_completed`: bài 2 chỉ mở sau khi bài 1 xong.
        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", unlock_rule = "after_previous_completed" });
        ch.EnsureSuccessStatusCode();
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // `is_required_to_unlock_next` là thứ THẬT SỰ khoá bài sau. Chỉ đặt luật chương thành
        // `after_previous_completed` là chưa đủ: `UnlockEngine.Evaluate` chỉ cho một bài chặn
        // bài kế tiếp khi bài đó được đánh dấu bắt buộc. Thiếu cờ này thì Bài 2 vẫn mở, và test
        // sẽ đỏ ở khẳng định "không lộ nội dung" — đỏ vì bài không hề bị khoá, không phải vì
        // bộ lọc hỏng.
        async Task<(Guid Lesson, Guid Component)> ThemBaiAsync(string ten, string biMat)
        {
            var b = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons",
                new { title = ten, is_required_to_unlock_next = true });
            b.EnsureSuccessStatusCode();
            var lessonId = (await b.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var cp = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new
            {
                type = "checkpoint_quiz",
                is_required = true,
                config = new
                {
                    html = $"<p>{biMat}</p>",
                    inline_questions = new object[]
                    {
                        new
                        {
                            text = $"Câu hỏi của {ten}",
                            options = new[] { "A", "B" },
                            correct_indexes = new[] { 1 },
                            explanation = biMat,
                        },
                    },
                },
            });
            cp.EnsureSuccessStatusCode();
            return (lessonId, (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var bai1 = await ThemBaiAsync("Bài 1", "NOI-DUNG-BAI-MOT");
        var bai2 = await ThemBaiAsync("Bài 2", "NOI-DUNG-BAI-HAI-BI-MAT");

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);
        var sv = await factory.LoginAsNewStudentOfAsync(offeringId);

        // ── Đường đọc ────────────────────────────────────────────────────────────────────
        var tienDo = await sv.GetAsync($"/api/offerings/{offeringId}/my-progress");
        tienDo.EnsureSuccessStatusCode();
        var tho = await tienDo.Content.ReadAsStringAsync();

        // Khẳng định trên CHUỖI JSON THÔ, không phải trên object đã phân giải: chuỗi thô là thứ
        // thật sự đi qua dây. Đọc qua DTO thì một trường mới bị quên lọc sẽ không ai thấy.
        tho.ShouldContain("NOI-DUNG-BAI-MOT",
            customMessage: "bài đã mở phải CÓ nội dung — thiếu dòng này thì test xanh vì lý do sai");
        tho.ShouldNotContain("NOI-DUNG-BAI-HAI-BI-MAT",
            customMessage: "bài đang khoá KHÔNG được gửi nội dung xuống");

        // Cấu trúc thì vẫn phải giữ: sinh viên cần biết bài khoá gồm mấy phần.
        var d = JsonDocument.Parse(tho).RootElement;
        var baiHai = d.GetProperty("chapters")[0].GetProperty("lessons")
            .EnumerateArray().Single(l => l.GetProperty("title").GetString() == "Bài 2");

        baiHai.GetProperty("access").GetString().ShouldBe("locked");
        baiHai.GetProperty("components").GetArrayLength().ShouldBe(1);
        baiHai.GetProperty("components")[0].GetProperty("inline_question_count").GetInt32().ShouldBe(1);
        baiHai.GetProperty("components")[0].GetProperty("inline_questions").GetArrayLength().ShouldBe(0);

        // ── Đường chấm ───────────────────────────────────────────────────────────────────
        var than = new { answers = new object[] { new { index = 0, selected_indexes = new[] { 1 } } } };

        var khoa = await sv.PostAsJsonAsync($"/api/lesson-components/{bai2.Component}/check", than);
        khoa.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await khoa.Content.ReadAsStringAsync()).ShouldNotContain("NOI-DUNG-BAI-HAI-BI-MAT");

        // Đối chứng dương trên CÙNG endpoint: thiếu bước này thì một route gõ sai cũng cho 404
        // và dòng trên xanh vĩnh viễn.
        var mo = await sv.PostAsJsonAsync($"/api/lesson-components/{bai1.Component}/check", than);
        mo.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
