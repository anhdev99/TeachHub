using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

/// <summary>
/// <c>OfferingStatus.Closed</c> phải là một CÁI KHOÁ, không phải một cái nhãn.
///
/// Trước loạt test này, <c>POST /api/offerings/{id}/close</c> đổi trạng thái và chốt một bản sổ
/// điểm, nhưng KHÔNG chỗ nào trong repo đọc <c>Status == Closed</c> — chỗ duy nhất đọc
/// <c>Status</c> là <c>AcceptsJoinCode</c>. Hệ quả: lớp đã kết thúc mà sinh viên vẫn nộp bài,
/// vẫn làm trắc nghiệm, vẫn đánh dấu tiến độ; bản chốt lúc đóng lớp đối chiếu với một sổ điểm
/// còn tiếp tục thay đổi sau lưng nó.
///
/// Mỗi test ở đây có ĐỐI CHỨNG DƯƠNG trên CÙNG một endpoint: cú gọi thành công lúc lớp còn
/// <c>Active</c> chạy trước, rồi mới tới cú 409 sau khi đóng. Thiếu vế thứ nhất thì một route
/// gõ sai cũng trả 409/404 và test xanh vĩnh viễn.
///
/// Và không chỉ khẳng định mã 409: mọi cú gọi bị chặn đều phải kèm <c>detail</c> nói rõ "đã kết
/// thúc". Nhiều nhánh nghiệp vụ khác cũng trả 409 (bài đã nộp chính thức, lượt làm đã nộp, thiếu
/// slot bắt buộc) — chỉ khẳng định con số 409 là nhận nhầm chúng làm bằng chứng cho cái khoá.
/// </summary>
[Collection(TeachHubCollection.Ten)]
public class ClosedOfferingLockTests(TeachHubApiFactory factory)
{
    private const string MatKhauSv = "Sinh@123";

    /// <summary>Mọi id của một lớp dựng đủ nội dung: bài học, bài tập, gợi ý, đề trắc nghiệm.</summary>
    private sealed record TheGioi(
        HttpClient Gv,
        HttpClient Sv,
        Guid OfferingId,
        string MaLop,
        Guid LessonId,
        Guid ComponentId,
        Guid CheckpointId,
        Guid AssignmentId,
        Guid Hint1,
        Guid Hint2,
        Guid QuizId);

    // ── Khẳng định dùng chung ───────────────────────────────────────────────────────────

    /// <summary>
    /// 409 KÈM lý do "đã kết thúc". Đọc <c>detail</c> chứ không chỉ đếm mã trạng thái: nếu phép
    /// kiểm lớp-đã-đóng bị đặt SAU một nhánh 409 khác (ví dụ "bài đã nộp chính thức nên không
    /// xóa tệp được"), test vẫn xanh trong khi cái khoá không hề chạy.
    /// </summary>
    private static async Task PhaiBiChanVeDongLopAsync(HttpResponseMessage res, string boiCanh)
    {
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict, customMessage: boiCanh);

        var than = await res.Content.ReadFromJsonAsync<JsonElement>();
        than.GetProperty("detail").GetString().ShouldNotBeNull(customMessage: boiCanh);
        than.GetProperty("detail").GetString()!
            .ShouldContain("kết thúc", customMessage: boiCanh);
    }

    private static async Task<HttpResponseMessage> PhaiThanhCongAsync(
        HttpResponseMessage res, string boiCanh)
    {
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Đối chứng dương hỏng ({boiCanh}): trả {(int)res.StatusCode} — " +
                await res.Content.ReadAsStringAsync());

        return res;
    }

    private static async Task DongLopAsync(HttpClient gv, Guid offeringId)
    {
        var res = await gv.PostAsync($"/api/offerings/{offeringId}/close", null);
        await PhaiThanhCongAsync(res, $"đóng lớp {offeringId}");

        var lop = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}");
        lop.GetProperty("status").GetString().ShouldBe("closed");
    }

    // ── Các đường GHI bị khoá ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Danh_dau_hoan_thanh_bai_hoc_va_phan_hoc_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        // Đối chứng dương: lớp còn Active thì cả hai endpoint đều chạy.
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/lessons/{tg.LessonId}/complete", null), "hoàn thành bài học");
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/lesson-components/{tg.ComponentId}/complete", null),
            "hoàn thành phần học");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        // Cùng endpoint, cùng id, chỉ khác trạng thái lớp. Đánh dấu lại vốn là thao tác
        // idempotent trả 200 — nên 409 ở đây chỉ có thể đến từ cái khoá.
        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/lessons/{tg.LessonId}/complete", null),
            "hoàn thành bài học sau khi đóng lớp");

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/lesson-components/{tg.ComponentId}/complete", null),
            "hoàn thành phần học sau khi đóng lớp");
    }

    [Fact]
    public async Task Cham_cau_hoi_nhung_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();
        var than = new { answers = new object[] { new { index = 0, selected_indexes = new[] { 0 } } } };

        await PhaiThanhCongAsync(
            await tg.Sv.PostAsJsonAsync($"/api/lesson-components/{tg.CheckpointId}/check", than),
            "chấm câu hỏi nhúng");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsJsonAsync($"/api/lesson-components/{tg.CheckpointId}/check", than),
            "chấm câu hỏi nhúng sau khi đóng lớp");
    }

    [Fact]
    public async Task Tai_tep_xoa_tep_va_nop_chinh_thuc_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();
        var url = $"/api/assignments/{tg.AssignmentId}/submissions/draft";

        // ── Đối chứng dương, lớp còn Active ──────────────────────────────────────────────
        await PhaiThanhCongAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "source_code", "bai-nop.zip", "application/zip", TaoZip()),
            "tải tệp ZIP lên");

        await PhaiThanhCongAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "images", "anh-1.png", "image/png", TaoPng()),
            "tải ảnh lên");

        var anh1 = await TepTheoTenAsync(tg.Sv, tg.AssignmentId, "anh-1.png");
        await PhaiThanhCongAsync(
            await tg.Sv.DeleteAsync($"/api/submission-files/{anh1}"), "xóa tệp nháp");

        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/assignments/{tg.AssignmentId}/submissions/submit", null),
            "nộp chính thức");

        // Tải thêm một tệp = mở lượt nộp lại. Cần bản NHÁP này để cú DELETE sau khi đóng lớp
        // không bị nhánh "bài đã nộp chính thức nên không xóa tệp được" trả lời hộ — nhánh đó
        // cũng là 409, và nó sẽ làm test xanh mà không chứng minh được gì.
        await PhaiThanhCongAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "images", "anh-2.png", "image/png", TaoPng()),
            "tải ảnh lên lượt nộp lại");

        var anh2 = await TepTheoTenAsync(tg.Sv, tg.AssignmentId, "anh-2.png");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        // ── Cùng ba endpoint đó, sau khi lớp đóng ────────────────────────────────────────
        await PhaiBiChanVeDongLopAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "images", "anh-3.png", "image/png", TaoPng()),
            $"tải tệp lên sau khi đóng lớp ({url})");

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.DeleteAsync($"/api/submission-files/{anh2}"),
            "xóa tệp nháp sau khi đóng lớp");

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/assignments/{tg.AssignmentId}/submissions/submit", null),
            "nộp chính thức sau khi đóng lớp");
    }

    [Fact]
    public async Task Mo_luot_luu_nhap_va_nop_trac_nghiem_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();
        var dapAn = new { answers = Array.Empty<object>() };

        // ── Đối chứng dương ──────────────────────────────────────────────────────────────
        var luot1 = await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/quizzes/{tg.QuizId}/attempts", null), "mở lượt trắc nghiệm");
        var luot1Id = (await luot1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PhaiThanhCongAsync(
            await tg.Sv.PutAsJsonAsync($"/api/quiz-attempts/{luot1Id}/answers", dapAn), "lưu nháp");

        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/quiz-attempts/{luot1Id}/submit", null), "nộp bài trắc nghiệm");

        // Lượt 2 để dở dang: sau khi đóng lớp còn một lượt ĐANG LÀM thật để thử lưu nháp và nộp.
        var luot2 = await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/quizzes/{tg.QuizId}/attempts", null), "mở lượt thứ hai");
        var luot2Id = (await luot2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await DongLopAsync(tg.Gv, tg.OfferingId);

        // ── Cùng ba endpoint đó ──────────────────────────────────────────────────────────
        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/quizzes/{tg.QuizId}/attempts", null),
            "mở lượt trắc nghiệm sau khi đóng lớp");

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PutAsJsonAsync($"/api/quiz-attempts/{luot2Id}/answers", dapAn),
            "lưu nháp sau khi đóng lớp");

        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/quiz-attempts/{luot2Id}/submit", null),
            "nộp bài trắc nghiệm sau khi đóng lớp");
    }

    [Fact]
    public async Task Mo_goi_y_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/assignment-hints/{tg.Hint1}/unlock", null), "mở gợi ý");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        // Gợi ý 2 chưa mở và mọi điều kiện khác đều thoả (gợi ý 1 đã mở, nên luật "mở lần lượt"
        // không chặn) — 409 ở đây chỉ có thể đến từ trạng thái lớp.
        await PhaiBiChanVeDongLopAsync(
            await tg.Sv.PostAsync($"/api/assignment-hints/{tg.Hint2}/unlock", null),
            "mở gợi ý sau khi đóng lớp");
    }

    /// <summary>
    /// Vào lớp bằng mã đã đúng sẵn qua <c>CourseOffering.AcceptsJoinCode</c> (đòi <c>Active</c>).
    /// Test này canh giữ nó: bỏ điều kiện trạng thái ra khỏi hàm đó là mở lại đường cho người
    /// mới ghi danh vào một lớp đã chốt sổ điểm.
    ///
    /// Cố ý KHÔNG phải 409: mã lớp là bí mật dùng chung và sẽ lan ra ngoài. Nếu "lớp đã đóng"
    /// trả khác "mã sai", kẻ dò mã phân biệt được hai ca và tìm ra mã hợp lệ chỉ bằng cách thử.
    /// </summary>
    [Fact]
    public async Task Vao_lop_bang_ma_bi_chan_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        var sv1 = await TaoSinhVienChuaGanAsync(tg.OfferingId);
        await PhaiThanhCongAsync(
            await sv1.PostAsJsonAsync("/api/offerings/join", new { join_code = tg.MaLop }), "vào lớp bằng mã");

        var sv2 = await TaoSinhVienChuaGanAsync(tg.OfferingId);

        await DongLopAsync(tg.Gv, tg.OfferingId);

        var res = await sv2.PostAsJsonAsync("/api/offerings/join", new { join_code = tg.MaLop });
        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Những gì CỐ Ý không khoá ────────────────────────────────────────────────────────

    /// <summary>
    /// Đóng lớp KHÔNG được cắt đường đọc của sinh viên. Họ vẫn phải xem lại được bài học, bài đã
    /// nộp, lượt trắc nghiệm và gợi ý đã mở sau khi lớp kết thúc — khoá đọc là xoá sổ toàn bộ giá
    /// trị của việc học.
    /// </summary>
    [Fact]
    public async Task Duong_doc_cua_sinh_vien_VAN_mo_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/lessons/{tg.LessonId}/complete", null), "hoàn thành bài học");
        await PhaiThanhCongAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "source_code", "bai-nop.zip", "application/zip", TaoZip()),
            "tải tệp ZIP lên");
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/assignments/{tg.AssignmentId}/submissions/submit", null),
            "nộp chính thức");
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/assignment-hints/{tg.Hint1}/unlock", null), "mở gợi ý");

        var luot = await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/quizzes/{tg.QuizId}/attempts", null), "mở lượt trắc nghiệm");
        var luotId = (await luot.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/quiz-attempts/{luotId}/submit", null), "nộp bài trắc nghiệm");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        (await tg.Sv.GetAsync($"/api/offerings/{tg.OfferingId}/my-progress"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tg.Sv.GetAsync($"/api/assignments/{tg.AssignmentId}/my-submission"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tg.Sv.GetAsync($"/api/offerings/{tg.OfferingId}/quizzes"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tg.Sv.GetAsync($"/api/quiz-attempts/{luotId}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Gợi ý đã mở vẫn đọc được nội dung: sinh viên đã trả giá bằng điểm cho nó.
        var goiY = await tg.Sv.GetFromJsonAsync<JsonElement>($"/api/assignments/{tg.AssignmentId}/hints");
        goiY.EnumerateArray()
            .Single(h => h.GetProperty("id").GetGuid() == tg.Hint1)
            .GetProperty("content").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Giảng viên KHÔNG bị khoá — quyết định có chủ ý, không phải bỏ sót. Chấm bài thường diễn ra
    /// SAU khi lớp kết thúc; chặn giảng viên chấm nghĩa là không ai dám đóng lớp và tính năng
    /// thành vô dụng. Bản chốt lúc đóng là ảnh chụp "tại thời điểm đóng", điểm đổi sau đó vẫn
    /// đối chiếu được với nó — đó chính là công dụng của nó.
    /// </summary>
    [Fact]
    public async Task Giang_vien_VAN_cham_va_tra_bai_duoc_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        await PhaiThanhCongAsync(
            await TaiLenAsync(tg.Sv, tg.AssignmentId, "source_code", "bai-nop.zip", "application/zip", TaoZip()),
            "tải tệp ZIP lên");
        await PhaiThanhCongAsync(
            await tg.Sv.PostAsync($"/api/assignments/{tg.AssignmentId}/submissions/submit", null),
            "nộp chính thức");

        var cua = await tg.Sv.GetFromJsonAsync<JsonElement>(
            $"/api/assignments/{tg.AssignmentId}/my-submission");
        var submissionId = cua.GetProperty("current").GetProperty("id").GetGuid();

        await DongLopAsync(tg.Gv, tg.OfferingId);

        var cham = await tg.Gv.PostAsJsonAsync($"/api/submissions/{submissionId}/grade", new
        {
            rubric_scores = new Dictionary<Guid, decimal>(),
            direct_score = 8.5m,
            feedback = "Chấm sau khi lớp đã kết thúc — đây là ca bình thường, không phải ngoại lệ.",
        });
        await PhaiThanhCongAsync(cham, "chấm bài sau khi đóng lớp");

        await PhaiThanhCongAsync(
            await tg.Gv.PostAsync($"/api/submissions/{submissionId}/return", null),
            "trả bài sau khi đóng lớp");
    }

    // ── Dựng thế giới ───────────────────────────────────────────────────────────────────

    private async Task<TheGioi> DungTheGioiAsync()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        // `free`: không có bài nào khoá bài nào. Test này canh cái khoá "lớp đã đóng", nên phải
        // loại hẳn cái khoá "bài chưa mở" ra khỏi bức tranh — hai cái khoá cùng chạy thì một
        // endpoint trả 404 vì lý do nào cũng làm test xanh nhầm.
        var nhanNenTang = $"nen-tang-{Guid.CreateVersion7().ToString("N")[^6..]}";

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", foundation_tags = new[] { nhanNenTang }, unlock_rule = "free" });
        await PhaiThanhCongAsync(ch, "tạo chương");
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "Bài 1" });
        await PhaiThanhCongAsync(b, "tạo bài học");
        var lessonId = (await b.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var hook = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components",
            new { type = "hook", config = new { html = "<p>Mở bài</p>" }, is_required = false });
        await PhaiThanhCongAsync(hook, "tạo phần học");
        var componentId = (await hook.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cp = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new
        {
            type = "checkpoint_quiz",
            is_required = false,
            config = new
            {
                inline_questions = new object[]
                {
                    new
                    {
                        text = "Đóng lớp có khoá đường ghi của sinh viên không?",
                        options = new[] { "Có", "Không" },
                        correct_indexes = new[] { 0 },
                        explanation = "Có — và đó chính là thứ loạt test này canh giữ.",
                    },
                },
            },
        });
        await PhaiThanhCongAsync(cp, "tạo phần checkpoint_quiz");
        var checkpointId = (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PhaiThanhCongAsync(
            await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null), "xuất bản phiên bản");

        // ── Lớp học phần ─────────────────────────────────────────────────────────────────
        var lop = await gv.PostAsJsonAsync("/api/offerings", new
        {
            subject_id = subjectId,
            subject_version_id = versionId,
            semester = "HK1 2026-2027",
            class_code = $"DK{Guid.CreateVersion7().ToString("N")[^5..].ToUpperInvariant()}",
        });
        await PhaiThanhCongAsync(lop, "mở lớp");
        var lopJson = await lop.Content.ReadFromJsonAsync<JsonElement>();
        var offeringId = lopJson.GetProperty("id").GetGuid();
        var maLop = lopJson.GetProperty("join_code").GetString()!;

        // ── Bài tập + gợi ý ──────────────────────────────────────────────────────────────
        var bt = await gv.PostAsJsonAsync("/api/assignments", new
        {
            subject_version_id = versionId,
            title = "Bài tập kiểm thử khoá đóng lớp",
            description = "Dữ liệu seed cho ClosedOfferingLockTests",
            due_at = (DateTimeOffset?)null,
            allow_late_submission = true,
            late_penalty_percent_per_day = 10m,
            max_score = 10m,
            use_suggested_slots = true,
        });
        await PhaiThanhCongAsync(bt, "tạo bài tập");
        var assignmentId = (await bt.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (hint1, hint2) = await TaoGoiYAsync(assignmentId);

        // ── Đề trắc nghiệm ───────────────────────────────────────────────────────────────
        var cauHoi = await gv.PostAsJsonAsync("/api/questions", new
        {
            id = (Guid?)null,
            type = "single_choice",
            text = "Lớp đã đóng thì sinh viên còn nộp bài được không?",
            explanation = (string?)null,
            foundation_tags = new[] { nhanNenTang },
            points = 1m,
            allow_partial_credit = false,
            options = new[]
            {
                new { text = "Không", is_correct = true },
                new { text = "Có", is_correct = false },
            },
            fill_blank = (object?)null,
        });
        await PhaiThanhCongAsync(cauHoi, "tạo câu hỏi");
        var questionId = (await cauHoi.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var deTn = await gv.PostAsJsonAsync("/api/quizzes", new
        {
            id = (Guid?)null,
            subject_version_id = versionId,
            offering_id = (Guid?)null,
            title = "Đề kiểm thử khoá đóng lớp",
            description = (string?)null,
            config = new
            {
                is_graded = true,
                time_limit_minutes = (int?)null,
                max_attempts = (int?)null,
                shuffle_questions = false,
                shuffle_options = false,
                show_explanation_after_submit = true,
            },
        });
        await PhaiThanhCongAsync(deTn, "tạo đề trắc nghiệm");
        var quizId = (await deTn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PhaiThanhCongAsync(
            await gv.PutAsJsonAsync($"/api/quizzes/{quizId}/questions", new { question_ids = new[] { questionId } }),
            "gắn câu hỏi vào đề");

        var sv = await factory.LoginAsNewStudentOfAsync(offeringId);

        return new TheGioi(
            gv, sv, offeringId, maLop, lessonId, componentId, checkpointId,
            assignmentId, hint1, hint2, quizId);
    }

    /// <summary>Hai gợi ý mở dần. Chưa có endpoint nào tạo được nên ghi thẳng vào DB.</summary>
    private async Task<(Guid Hint1, Guid Hint2)> TaoGoiYAsync(Guid assignmentId)
    {
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var g1 = new AssignmentHint
        {
            AssignmentId = assignmentId, OrderIndex = 0,
            Content = "Gợi ý 1: đọc kỹ yêu cầu về cấu trúc thư mục.", PenaltyPercent = 10m,
        };

        var g2 = new AssignmentHint
        {
            AssignmentId = assignmentId, OrderIndex = 1,
            Content = "Gợi ý 2: bắt đầu từ hàm main.", PenaltyPercent = 20m,
        };

        db.AssignmentHints.Add(g1);
        db.AssignmentHints.Add(g2);
        await db.SaveChangesAsync();

        return (g1.Id, g2.Id);
    }

    /// <summary>
    /// Tài khoản sinh viên CÓ TÊN trong danh sách lớp nhưng CHƯA gắn <c>UserId</c> — đúng trạng
    /// thái mà luồng "vào lớp bằng mã" phục vụ. Dùng <c>LoginAsNewStudentOfAsync</c> thay thế
    /// không được: nó gắn sẵn <c>UserId</c>, và cú join khi đó không đi qua đường thật.
    /// </summary>
    private async Task<HttpClient> TaoSinhVienChuaGanAsync(Guid offeringId)
    {
        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var email = $"vaolop-{duoi}@sv.edu.vn";

        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var u = new AppUser
            {
                UserName = email, Email = email, FullName = $"SV {duoi}",
                StudentCode = $"22560{duoi}", EmailConfirmed = true,
            };

            var tao = await users.CreateAsync(u, MatKhauSv);
            if (!tao.Succeeded)
                throw new InvalidOperationException(
                    $"Seed thất bại: không tạo được tài khoản {email} — " +
                    string.Join("; ", tao.Errors.Select(e => e.Description)));

            await users.AddToRoleAsync(u, Roles.Student);

            db.Enrollments.Add(new Enrollment
            {
                OfferingId = offeringId, StudentCode = u.StudentCode!, FullName = u.FullName,
                Email = email, UserId = null, Status = EnrollmentStatus.Imported,
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var dn = await client.PostAsJsonAsync("/api/auth/login", new { email, password = MatKhauSv });
        await PhaiThanhCongAsync(dn, $"đăng nhập {email}");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await dn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString());

        return client;
    }

    // ── Nộp tệp ─────────────────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> TaiLenAsync(
        HttpClient sv, Guid assignmentId, string slotKey, string tenTep, string contentType, byte[] noiDung)
    {
        // `await` trước khi ra khỏi khối `using`: trả thẳng Task về thì form bị Dispose trong lúc
        // request còn đang gửi, và lỗi hiện ra dưới dạng một 400 khó hiểu chứ không phải lỗi thật.
        using var form = new MultipartFormDataContent();

        var tep = new ByteArrayContent(noiDung);
        tep.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(tep, "file", tenTep);

        // "slot_key" ghim tường minh: model binder của form KHÔNG theo JsonNamingPolicy.
        form.Add(new StringContent(slotKey), "slot_key");

        return await sv.PostAsync($"/api/assignments/{assignmentId}/submissions/draft", form);
    }

    private static async Task<Guid> TepTheoTenAsync(HttpClient sv, Guid assignmentId, string tenTep)
    {
        var cua = await sv.GetFromJsonAsync<JsonElement>($"/api/assignments/{assignmentId}/my-submission");

        return cua.GetProperty("current").GetProperty("files").EnumerateArray()
            .Single(f => f.GetProperty("file_name").GetString() == tenTep)
            .GetProperty("id").GetGuid();
    }

    /// <summary>ZIP thật, có README.md và src/ đúng như slot gợi ý đòi hỏi.</summary>
    private static byte[] TaoZip()
    {
        using var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("README.md").Open()))
                w.Write("# Bài nộp seed cho ClosedOfferingLockTests\n");

            using (var w = new StreamWriter(zip.CreateEntry("src/Program.cs").Open()))
                w.Write("// mã nguồn seed\n");
        }

        return ms.ToArray();
    }

    /// <summary>PNG 8×8 hợp lệ — FileSignatureValidator đọc 8 byte đầu nên byte bịa ra sẽ bị chặn.</summary>
    private static byte[] TaoPng()
    {
        using var anh = new Image<Rgba32>(8, 8);
        using var ms = new MemoryStream();
        anh.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Vào nhóm cũng là một đường GHI của sinh viên vào lớp, dù nó nằm ở feature khác.
    ///
    /// Thiếu phép kiểm này thì sinh viên vẫn đổi được cơ cấu nhóm của một lớp đã chốt sổ điểm —
    /// tức đổi cả cách chia điểm nhóm sau khi bản chốt đã ký.
    /// </summary>
    [Fact]
    public async Task Vao_nhom_bi_khoa_sau_khi_dong_lop()
    {
        var tg = await DungTheGioiAsync();

        // Nhóm KHÔNG được rỗng (validator: "Nhóm rỗng thì không có gì để chấm"), nên gieo nó
        // bằng chính sinh viên có sẵn của thế giới test.
        var dsGhiDanh = await tg.Gv.GetFromJsonAsync<JsonElement>(
            $"/api/offerings/{tg.OfferingId}/enrollments");
        var ghiDanhDauTien = dsGhiDanh.EnumerateArray().First().GetProperty("id").GetGuid();

        var taoNhom = await tg.Gv.PostAsJsonAsync($"/api/offerings/{tg.OfferingId}/groups",
            new { name = "Nhóm 1", member_enrollment_ids = new[] { ghiDanhDauTien } });
        taoNhom.EnsureSuccessStatusCode();
        var nhomId = (await taoNhom.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // `LoginAsNewStudentOfAsync` chứ không phải `TaoSinhVienChuaGanAsync`: hàm kia gieo
        // ghi danh với `UserId = null` (dành cho luồng vào lớp bằng mã), nên handler tra theo
        // `e.UserId == request.UserId` không thấy gì và trả 404 — đối chứng dương hỏng vì lý do
        // chẳng liên quan gì tới cái khoá.
        //
        // Tạo TRƯỚC khi đóng lớp: sau khi đóng thì không thêm được sinh viên nào nữa.
        var sv2 = await factory.LoginAsNewStudentOfAsync(tg.OfferingId);
        var sv3 = await factory.LoginAsNewStudentOfAsync(tg.OfferingId);

        // Đối chứng dương: lớp còn Active thì vào nhóm được.
        await PhaiThanhCongAsync(await sv2.PostAsync($"/api/groups/{nhomId}/join", null), "vào nhóm");

        await DongLopAsync(tg.Gv, tg.OfferingId);

        // Người thử là sinh viên CHƯA có nhóm, nên 409 không thể đến từ nhánh "đã ở trong một
        // nhóm của lớp này" — nhánh đó cũng trả 409, và nhận nhầm nó là bỏ sót cái khoá.
        await PhaiBiChanVeDongLopAsync(
            await sv3.PostAsync($"/api/groups/{nhomId}/join", null), "vào nhóm sau khi đóng lớp");
    }
}
