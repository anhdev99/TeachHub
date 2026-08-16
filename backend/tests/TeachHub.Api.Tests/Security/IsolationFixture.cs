using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Submissions;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests.Security;

/// <summary>
/// Dựng "thế giới của giảng viên A" rồi giao cho ma trận ba client: chính A, một giảng viên B
/// hoàn toàn độc lập, và một tài khoản chỉ có vai trò trợ giảng.
///
/// Phase nào thêm một loại tài nguyên mới thuộc về giảng viên thì seed thêm ở đây VÀ thêm dòng
/// vào ma trận — đó là điều kiện của Definition of Done mục 7, không phải việc tuỳ tâm.
/// Thế giới của A hiện trải từ Phase 0 tới Phase 7: lời mời, môn/chương/bài, lớp học phần,
/// bài tập + slot + bài nộp, nhóm, đợt cuối môn, rubric, sổ điểm + điểm cộng + các bộ mẫu,
/// ngân hàng câu hỏi + đề trắc nghiệm, quy tắc XP và huy hiệu.
/// </summary>
public sealed class IsolationFixture : TeachHubApiFactory
{
    public HttpClient GvA { get; private set; } = null!;
    public HttpClient GvB { get; private set; } = null!;
    public HttpClient TroGiang { get; private set; } = null!;

    /// <summary>Id các tài nguyên THUỘC VỀ giảng viên A, để ma trận thay vào template.</summary>
    private readonly Dictionary<string, string> _idCuaA = [];

    private const string MatKhau = "Teach@123";
    private const string EmailB  = "gv-b@teachhub.local";
    private const string EmailTa = "tro-giang@teachhub.local";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        GvA = await LoginAsInstructorAsync();

        // ---- Thế giới của A: một lời mời do A tạo ----
        var invite = await GvA.PostAsJsonAsync("/api/instructor/invites",
            new { email = "nguoi-duoc-a-moi@teachhub.local" });
        invite.EnsureSuccessStatusCode();
        var inviteJson = await invite.Content.ReadFromJsonAsync<JsonElement>();
        _idCuaA["inviteId"] = inviteJson.GetProperty("id").GetString()!;

        // ---- Thế giới nội dung của A: môn → phiên bản → chương → bài → component ----
        var (subjectId, versionId) = await SeedSubjectWithDraftAsync(GvA);
        _idCuaA["subjectId"] = subjectId.ToString();
        _idCuaA["versionId"] = versionId.ToString();

        var chuong = await GvA.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương của A" });
        chuong.EnsureSuccessStatusCode();
        var chapterId = (await chuong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        _idCuaA["chapterId"] = chapterId.ToString();

        var bai = await GvA.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons",
            new { title = "Bài của A" });
        bai.EnsureSuccessStatusCode();
        var lessonId = (await bai.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        _idCuaA["lessonId"] = lessonId.ToString();

        var cp = await GvA.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new { type = "hook" });
        cp.EnsureSuccessStatusCode();
        _idCuaA["componentId"] =
            (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // ---- Lớp học phần của A (Phase 2) ----
        // Dùng một MÔN RIÊNG, không dùng lại môn ở trên: mở lớp đòi phiên bản đã xuất bản,
        // mà xuất bản thì [RequireDraftVersion] sẽ khiến các dòng sửa nội dung ở trên trả 409
        // thay vì 404 — ma trận khi đó không còn kiểm được lớp phân quyền nữa.
        var (subjectChoLop, versionChoLop) = await SeedPublishedSubjectAsync(GvA);
        var offeringId = await CreateOfferingAsync(GvA, subjectChoLop, versionChoLop);
        _idCuaA["offeringId"] = offeringId.ToString();

        // Phiên bản mà LỚP đang ghim. Bài tập và đề trắc nghiệm phải nằm trên đúng phiên bản
        // này, nếu không báo cáo trùng lặp (Phase 6) sẽ trả 404 vì "bài tập không thuộc lớp"
        // và dòng ma trận khi đó xanh vì lý do sai.
        _idCuaA["offeringVersionId"] = versionChoLop.ToString();

        await SeedBaiTapVaBaiNopAsync(offeringId, versionChoLop);
        await SeedChamDiemVaSoDiemAsync(offeringId);
        await SeedNganHangVaDongLucAsync(offeringId, versionChoLop);

        // ---- Giảng viên B: độc lập hoàn toàn, được tạo qua đúng luồng lời mời ----
        var moiB = await GvA.PostAsJsonAsync("/api/instructor/invites", new { email = EmailB });
        var tokenB = (await moiB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        (await CreateClient().PostAsJsonAsync("/api/auth/register",
            new { token = tokenB, full_name = "Giảng viên B", password = MatKhau }))
            .EnsureSuccessStatusCode();
        GvB = await DangNhapAsync(EmailB);

        // ---- Trợ giảng: vai trò tồn tại nhưng KHÔNG có policy nào cấp quyền (ADR-013) ----
        using (var scope = CreateServiceScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            if (!await roles.RoleExistsAsync(Roles.TeachingAssistant))
                await roles.CreateAsync(new AppRole(Roles.TeachingAssistant));

            var ta = new AppUser
            {
                UserName = EmailTa, Email = EmailTa,
                FullName = "Trợ giảng", EmailConfirmed = true,
            };
            await users.CreateAsync(ta, MatKhau);
            await users.AddToRoleAsync(ta, Roles.TeachingAssistant);
        }
        TroGiang = await DangNhapAsync(EmailTa);
    }

    // ── Phase 3: bài tập, slot nộp, sinh viên, bài nộp ──────────────────────────────────

    private async Task SeedBaiTapVaBaiNopAsync(Guid offeringId, Guid versionChoLop)
    {
        var baiTap = await TaoAsync(GvA, "/api/assignments", new
        {
            subject_version_id = versionChoLop,
            title = "Bài tập của A",
            description = "Dữ liệu seed cho ma trận cách ly",
            due_at = (DateTimeOffset?)null,
            allow_late_submission = true,
            late_penalty_percent_per_day = 10m,
            max_score = 10m,
            use_suggested_slots = true,          // kèm luôn ba slot mẫu ⇒ có slotId để kiểm
        });

        var assignmentId = baiTap.GetProperty("id").GetGuid();
        _idCuaA["assignmentId"] = assignmentId.ToString();

        var slot = baiTap.GetProperty("slots").EnumerateArray().First();
        var slotId = slot.GetProperty("id").GetGuid();
        _idCuaA["slotId"] = slotId.ToString();

        // Hai sinh viên: bắt buộc phải là HAI, không phải một. Endpoint so sánh hai bài nộp
        // trả 400 "không so sánh một bài với chính nó" TRƯỚC khi kiểm quyền sở hữu, nên một
        // bài nộp duy nhất sẽ làm dòng /compare/ xanh vì lý do hoàn toàn khác.
        _ = await LoginAsNewStudentOfAsync(offeringId);
        _ = await LoginAsNewStudentOfAsync(offeringId);

        var ghiDanh = await GvA.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        var enrollmentIds = ghiDanh.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        _idCuaA["enrollmentId"] = enrollmentIds[0].ToString();

        // Bài nộp ghi thẳng qua DbContext thay vì đi qua luồng upload multipart: luồng thật
        // đòi một tệp ZIP có chữ ký hợp lệ và một storage chạy được, mà thứ ma trận cần chỉ là
        // MỘT id bài nộp thuộc về lớp của A. Buộc seed đi qua storage là gắn cổng bảo mật này
        // vào một phụ thuộc hạ tầng không liên quan.
        var now = TimeProvider.System.GetUtcNow();

        using var scope = CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var idBaiNop = new List<Guid>();
        var idTepDauTien = Guid.Empty;

        foreach (var enrollmentId in enrollmentIds)
        {
            var baiNop = new Submission
            {
                OwnerType = SlotOwnerType.Assignment,
                AssignmentId = assignmentId,
                OfferingId = offeringId,
                EnrollmentId = enrollmentId,
                SubmittedByType = SubmittedByType.Individual,
                AttemptNo = 1,
                IsCurrent = true,
                Status = SubmissionStatus.Submitted,
                SubmittedAt = now,
            };

            var tep = new SubmissionFile
            {
                SubmissionId = baiNop.Id,
                SlotId = slotId,
                SlotKey = slot.GetProperty("slot_key").GetString()!,
                FileName = "bai-nop.zip",
                FilePath = $"submissions/{baiNop.Id}/bai-nop.zip",
                SizeBytes = 2048,
                ContentType = "application/zip",
                UploadedAt = now,
                ProcessingStatus = FileProcessingStatus.Done,
            };

            db.Submissions.Add(baiNop);
            db.SubmissionFiles.Add(tep);

            if (idBaiNop.Count == 0) idTepDauTien = tep.Id;
            idBaiNop.Add(baiNop.Id);
        }

        await db.SaveChangesAsync();

        _idCuaA["submissionId"] = idBaiNop[0].ToString();
        _idCuaA["submissionId2"] = idBaiNop[1].ToString();
        _idCuaA["submissionFileId"] = idTepDauTien.ToString();

        // ---- Nhóm của lớp A (Phase 4a) ----
        var nhom = await TaoAsync(GvA, $"/api/offerings/{offeringId}/groups", new
        {
            name = "Nhóm 1 của A",
            member_enrollment_ids = new[] { enrollmentIds[0] },
        });
        _idCuaA["groupId"] = nhom.GetProperty("id").GetGuid().ToString();

        // ---- Đợt chấm cuối môn (Phase 4a) ----
        var dot = await TaoAsync(GvA, $"/api/offerings/{offeringId}/final-assessments", new
        {
            type = "final_project",
            title = "Đồ án cuối môn của A",
            description = (string?)null,
            grade_column_id = (Guid?)null,
            due_at = (DateTimeOffset?)null,
            submission_mode = "individual",
            max_score = 10m,
            use_suggested_slots = true,
        });
        _idCuaA["finalAssessmentId"] = dot.GetProperty("id").GetGuid().ToString();
    }

    // ── Phase 4a/4b: rubric, sổ điểm, điểm cộng, các bộ mẫu ─────────────────────────────

    private async Task SeedChamDiemVaSoDiemAsync(Guid offeringId)
    {
        var rubric = await TaoAsync(GvA, "/api/rubrics", new
        {
            name = "Rubric của A",
            is_template = true,
            criteria = new[] { new { description = "Đúng yêu cầu đề bài", max_points = 10m } },
        });
        _idCuaA["rubricId"] = rubric.GetProperty("id").GetGuid().ToString();

        await ChayAsync(GvA, $"/api/offerings/{offeringId}/gradebook/init",
            new { template_id = (Guid?)null });

        var soDiem = await GvA.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/gradebook");
        _idCuaA["gradeColumnId"] = soDiem.GetProperty("columns").EnumerateArray()
            .First().GetProperty("id").GetGuid().ToString();

        // Chốt sổ ngay sau khi khởi tạo cột: bộ cột mặc định có trọng số 40/60 nên tổng đúng
        // 100% — chốt sổ khi trọng số chưa hợp lệ sẽ trả 409 và seed chết.
        var banChot = await TaoAsync(GvA, $"/api/offerings/{offeringId}/gradebook/snapshot", new { });
        _idCuaA["snapshotId"] = banChot.GetProperty("id").GetGuid().ToString();

        var diemCong = await TaoAsync(GvA, "/api/bonus-points", new
        {
            enrollment_id = Guid.Parse(_idCuaA["enrollmentId"]),
            target = "final_total",
            target_column_id = (Guid?)null,
            points = 1m,
            reason = "Phát biểu tốt trong buổi 3",
            template_id = (Guid?)null,
        });
        _idCuaA["bonusPointId"] = diemCong.GetProperty("id").GetGuid().ToString();

        var lyDo = await TaoAsync(GvA, "/api/instructor/bonus-reasons", new
        {
            id = (Guid?)null,
            label = "Phát biểu tốt",
            default_points = 0.5m,
            default_target = "final_total",
            default_column_key = (string?)null,
        });
        _idCuaA["bonusReasonId"] = lyDo.GetProperty("id").GetGuid().ToString();

        var boCot = await TaoAsync(GvA, "/api/instructor/grade-scheme-templates", new
        {
            id = (Guid?)null,
            name = "Bộ cột của A",
            is_default = false,
            items = new[]
            {
                new
                {
                    key = "thuong_ky", name = "Thường kỳ", weight_percent = 100m,
                    order_index = 0, kind = "manual", n = 0, treat_missing_as_zero = false,
                },
            },
        });
        _idCuaA["schemeTemplateId"] = boCot.GetProperty("id").GetGuid().ToString();

        var mauXuat = await TaoAsync(GvA, "/api/instructor/grade-export-templates", new
        {
            id = (Guid?)null,
            name = "Mẫu xuất của A",
            is_default = false,
            items = new[]
            {
                new
                {
                    source = "student_code", column_key = (string?)null,
                    header = "MSSV", order_index = 0, decimal_places = 0,
                },
            },
        });
        _idCuaA["exportTemplateId"] = mauXuat.GetProperty("id").GetGuid().ToString();
    }

    // ── Phase 5/7: ngân hàng câu hỏi, đề trắc nghiệm, quy tắc XP, huy hiệu ──────────────

    private async Task SeedNganHangVaDongLucAsync(Guid offeringId, Guid versionChoLop)
    {
        var cauHoi = await TaoAsync(GvA, "/api/questions", new
        {
            id = (Guid?)null,
            type = "single_choice",
            text = "Câu hỏi trong ngân hàng của A",
            explanation = (string?)null,
            foundation_tags = new[] { "seed" },
            points = 1m,
            allow_partial_credit = false,
            options = new[]
            {
                new { text = "Đúng", is_correct = true },
                new { text = "Sai", is_correct = false },
            },
            fill_blank = (object?)null,
        });
        _idCuaA["questionId"] = cauHoi.GetProperty("id").GetGuid().ToString();

        var quiz = await TaoAsync(GvA, "/api/quizzes", new
        {
            id = (Guid?)null,
            subject_version_id = versionChoLop,
            offering_id = (Guid?)null,
            title = "Bài trắc nghiệm của A",
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
        _idCuaA["quizId"] = quiz.GetProperty("id").GetGuid().ToString();

        // Gieo bảng quy tắc XP mặc định rồi lấy một quy tắc bất kỳ làm mục tiêu cho ma trận.
        var quyTac = await TaoAsync(GvA, $"/api/offerings/{offeringId}/xp-rules/seed", new { });
        _idCuaA["xpRuleId"] = quyTac.EnumerateArray().First().GetProperty("id").GetGuid().ToString();

        var huyHieu = await TaoAsync(GvA, "/api/badges", new
        {
            id = (Guid?)null,
            code = "sieu_nhan_cua_a",
            name = "Siêu nhân của A",
            description = (string?)null,
            icon = (string?)null,
            logic = "all",
            is_enabled = true,
            conditions = new[]
            {
                new { metric = "total_xp", comparison = "at_least", threshold = 100m },
            },
        });
        _idCuaA["badgeId"] = huyHieu.GetProperty("id").GetGuid().ToString();
    }

    // ── Hạ tầng seed ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST rồi trả về thân JSON. Không dùng <c>EnsureSuccessStatusCode</c> trần: seed hỏng vì
    /// một trường thiếu trong body sẽ chỉ hiện "400 Bad Request" và người sửa phải đoán trường
    /// nào — in cả thân ProblemDetails ra là biết ngay validator nào đã chặn.
    /// </summary>
    private static async Task<JsonElement> TaoAsync(HttpClient client, string url, object body)
    {
        var res = await client.PostAsJsonAsync(url, body);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: POST {url} trả {(int)res.StatusCode} — {await res.Content.ReadAsStringAsync()}");

        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Như <see cref="TaoAsync"/> nhưng cho endpoint trả 200 với thân rỗng.</summary>
    private static async Task ChayAsync(HttpClient client, string url, object body)
    {
        var res = await client.PostAsJsonAsync(url, body);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: POST {url} trả {(int)res.StatusCode} — {await res.Content.ReadAsStringAsync()}");
    }

    private async Task<HttpClient> DangNhapAsync(string email)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = MatKhau });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
        return client;
    }

    /// <summary>Thay <c>{inviteId}</c>… bằng id thật của A, không kiểm phần còn lại.</summary>
    private string ThayId(string s)
    {
        foreach (var (khoa, giaTri) in _idCuaA)
            s = s.Replace($"{{{khoa}}}", giaTri, StringComparison.Ordinal);

        return s;
    }

    /// <summary>Thay <c>{inviteId}</c>… trong template URL bằng id thật của giảng viên A.</summary>
    public string Resolve(string template)
    {
        var url = ThayId(template);

        if (url.Contains('{', StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Template '{template}' còn placeholder chưa được seed trong IsolationFixture: {url}");

        return url;
    }

    /// <summary>
    /// Thay placeholder trong THÂN request JSON. Không dùng lại <see cref="Resolve"/> được:
    /// thân JSON đầy dấu <c>{</c> của chính cú pháp JSON, nên phép kiểm "còn ngoặc là còn
    /// placeholder" sẽ báo động nhầm ở mọi dòng.
    /// </summary>
    public string ResolveBody(string body)
    {
        var s = ThayId(body);

        // Trong JSON, sau '{' luôn là '"' hoặc khoảng trắng hoặc '}'. '{' đứng ngay trước một
        // CHỮ CÁI chỉ có thể là placeholder chưa seed — bắt tại đây thay vì để nó đi tiếp và
        // thành một lỗi 400 khó hiểu ở tầng đọc JSON, tức dòng ma trận đỏ vì lý do sai.
        for (var i = 0; i < s.Length - 1; i++)
            if (s[i] == '{' && char.IsLetter(s[i + 1]))
                throw new InvalidOperationException(
                    $"Thân request còn placeholder chưa được seed trong IsolationFixture: {body}");

        return s;
    }
}
