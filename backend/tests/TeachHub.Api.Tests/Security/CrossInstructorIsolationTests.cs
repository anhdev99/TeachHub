using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests.Security;

[Collection(IsolationCollection.Ten)]
public sealed class CrossInstructorIsolationTests(IsolationFixture fx)
{
    /// <summary>
    /// Mỗi dòng là một tài nguyên THUỘC VỀ giảng viên A; client bắn vào là của giảng viên B.
    /// Phase nào thêm loại tài nguyên mới thì thêm dòng vào đây — đó là điều kiện của
    /// Definition of Done mục 7, không phải việc tuỳ tâm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Body</c> là JSON hợp lệ cho method có thân request. BẮT BUỘC phải có: gửi POST/PUT
    /// không kèm body thì request dừng ở 400 vì model binding, tức test đỏ vì lý do hoàn toàn
    /// khác chứ không kiểm được lớp phân quyền. Body phải đủ hợp lệ để đi qua cả validator —
    /// ValidationBehavior chạy TRƯỚC handler, nên một trường sai luật cũng trả 400 và dòng đó
    /// không bao giờ chạm tới phép kiểm quyền sở hữu.
    /// </para>
    /// <para>
    /// Placeholder <c>{…}</c> trong body cũng được thay bằng id thật của A. Đó là chủ ý: gửi
    /// một GUID ngẫu nhiên thì handler có thể trả 404 chỉ vì "không có bản ghi này" — dòng test
    /// xanh mà không chứng minh được gì. Gửi ĐÚNG enrollment_id, đúng column_id của lớp A mới
    /// ép được handler trả lời câu hỏi thật: "người gọi này có quyền trên tài nguyên đó không".
    /// </para>
    /// <para>
    /// KHÔNG có mặt ở đây, có lý do:
    /// endpoint chỉ dành cho sinh viên (<c>Policies.Student</c>) — nộp bài, mở gợi ý, làm
    /// trắc nghiệm, ôn tập, recap của Phase 8 — vì giảng viên B bị chặn ngay ở lớp policy và
    /// nhận 403, không phải 404; 403 ở đó không tiết lộ gì về dữ liệu của A vì nó phát sinh
    /// TRƯỚC khi handler đọc bất kỳ id nào. Rò rỉ chéo ở nhánh sinh viên là một mối đe dọa
    /// khác (sinh viên lớp khác), cần một ma trận riêng chứ không phải ma trận này.
    /// Hai endpoint upload — <c>POST /api/offerings/{id}/enrollments/import</c> và
    /// <c>POST /api/lessons/{id}/file</c> — cũng vắng mặt: chúng khai
    /// <c>[Consumes("multipart/form-data")]</c> nên thân JSON trả 415 TRƯỚC khi tới handler,
    /// tức dòng test sẽ đỏ vì content-type chứ không vì phân quyền. Phép kiểm sở hữu của
    /// chúng là <c>OwnsOfferingAsync</c> và <c>OwnsLessonAsync</c> — đúng hai phép kiểm mà
    /// hơn hai mươi dòng khác trong bảng này đã canh trên cùng lớp học phần và cùng bài học.
    /// <c>GET /api/files/{**path}</c> vắng mặt vì nó định danh bằng ĐƯỜNG DẪN chứ không bằng
    /// id: chỉ seed được bằng một lần upload thật, và một đường dẫn bịa ra sẽ trả 404 cho cả
    /// giảng viên A — dòng test khi đó xanh mà không chứng minh được gì.
    /// </para>
    /// </remarks>
    internal static readonly (string Method, string Template, string? Body)[] TaiNguyen =
    [
        ("DELETE", "/api/instructor/invites/{inviteId}", null),

        // Phase 1
        ("GET",    "/api/subjects/{subjectId}",                    null),
        ("GET",    "/api/subjects/{subjectId}/versions",           null),
        ("POST",   "/api/subjects/{subjectId}/versions",           "{\"version_label\":\"ban-cua-b\"}"),
        ("PUT",    "/api/subject-versions/{versionId}",            "{\"changelog\":\"b sua len\"}"),
        ("POST",   "/api/subject-versions/{versionId}/publish",    null),
        ("GET",    "/api/subject-versions/{versionId}/chapters",   null),
        ("POST",   "/api/subject-versions/{versionId}/chapters",   "{\"title\":\"chuong cua b\"}"),
        ("GET",    "/api/chapters/{chapterId}/lessons",            null),
        ("POST",   "/api/chapters/{chapterId}/lessons",            "{\"title\":\"bai cua b\"}"),
        ("DELETE", "/api/chapters/{chapterId}",                    null),
        ("GET",    "/api/lessons/{lessonId}",                      null),
        ("PUT",    "/api/lessons/{lessonId}/reorder",              "{\"to_index\":0}"),
        ("POST",   "/api/lessons/{lessonId}/components",           "{\"type\":\"hook\"}"),
        ("DELETE", "/api/lesson-components/{componentId}",         null),
        ("PUT",    "/api/lesson-components/{componentId}/reorder", "{\"to_index\":0}"),

        // Phase 2
        // Mở lớp TRÊN MÔN CỦA NGƯỜI KHÁC: id của A nằm trong body chứ không trong URL. Quên
        // kiểm ở đây thì B mở được lớp trên môn của A, và lớp đó ghim đúng phiên bản nội dung
        // của A — toàn bộ bài giảng chảy sang một lớp mà A không biết mình đang "dạy".
        ("POST",   "/api/offerings",
            """{"subject_id":"{subjectId}","semester":"HK1 2026-2027","class_code":"LOP-CUA-B","subject_version_id":null,"time_zone_id":null}"""),
        ("GET",    "/api/offerings/{offeringId}",                          null),
        ("GET",    "/api/offerings/{offeringId}/enrollments",              null),
        ("POST",   "/api/offerings/{offeringId}/enrollments/create-accounts", null),

        // ── Phase 3: bài tập, slot nộp, bài nộp, cây thư mục trong tệp nén ──────────────
        // Dòng đầu là ca "tạo mới trong không gian của người khác": không có id nào của A
        // trong URL, chỉ có subject_version_id trong body — quên kiểm ở đó thì B ra được đề
        // bài trong môn của A, và sinh viên lớp A nhìn thấy nó như bài tập chính thức.
        ("POST",   "/api/assignments",
            """{"subject_version_id":"{offeringVersionId}","title":"B tao bai trong mon cua A","description":null,"due_at":null,"allow_late_submission":true,"late_penalty_percent_per_day":10,"max_score":10,"use_suggested_slots":false}"""),
        ("GET",    "/api/assignments/{assignmentId}", null),
        ("PUT",    "/api/assignments/{assignmentId}",
            """{"title":"B sua bai cua A","description":null,"due_at":null,"allow_late_submission":true,"late_penalty_percent_per_day":10,"max_score":10,"rubric_id":null}"""),
        ("GET",    "/api/subject-versions/{offeringVersionId}/assignments", null),
        ("POST",   "/api/assignments/{assignmentId}/slots",
            """{"slot_key":"slot_cua_b","label":"Slot cua B","accepted_file_types":[".zip"],"allow_link_instead_of_file":false,"max_files":1,"max_file_size_mb":10,"is_required":true,"required_files":[],"required_folders":[]}"""),
        ("POST",   "/api/assignments/{assignmentId}/slots/suggested", null),
        ("PUT",    "/api/submission-slots/{slotId}",
            """{"label":"B doi nhan slot cua A","accepted_file_types":[".zip"],"allow_link_instead_of_file":false,"max_files":1,"max_file_size_mb":10,"is_required":true,"required_files":[],"required_folders":[]}"""),
        ("PUT",    "/api/submission-slots/{slotId}/reorder", """{"to_index":0}"""),
        ("DELETE", "/api/submission-slots/{slotId}", null),
        ("GET",    "/api/submissions/{submissionId}", null),
        ("GET",    "/api/submissions/{submissionId}/files/{submissionFileId}/file-tree", null),
        ("GET",    "/api/submissions/{submissionId}/files/{submissionFileId}/entry?path=README.md", null),

        // ── Phase 4a: nhóm, chấm bài, trả bài, đợt cuối môn ─────────────────────────────
        ("GET",    "/api/offerings/{offeringId}/groups", null),
        ("POST",   "/api/offerings/{offeringId}/groups",
            """{"name":"Nhom cua B","member_enrollment_ids":["{enrollmentId}"]}"""),
        ("POST",   "/api/offerings/{offeringId}/groups/auto-generate",
            """{"group_size":2,"seed":7,"preview":true}"""),
        ("PUT",    "/api/groups/{groupId}/members",
            """{"member_enrollment_ids":["{enrollmentId}"]}"""),
        ("POST",   "/api/submissions/{submissionId}/grade",
            """{"rubric_scores":{},"direct_score":9.5,"feedback":"B cham bai cua lop A"}"""),
        ("POST",   "/api/submissions/{submissionId}/return", null),
        ("GET",    "/api/offerings/{offeringId}/submissions", null),
        ("GET",    "/api/offerings/{offeringId}/final-assessments", null),
        ("POST",   "/api/offerings/{offeringId}/final-assessments",
            """{"type":"final_project","title":"Dot cuoi mon do B tao","description":null,"grade_column_id":null,"due_at":null,"submission_mode":"individual","max_score":10,"use_suggested_slots":false}"""),
        ("POST",   "/api/final-assessments/{finalAssessmentId}/slots",
            """{"slot_key":"slot_cua_b","label":"Slot cua B","accepted_file_types":[".zip"],"allow_link_instead_of_file":false,"max_files":1,"max_file_size_mb":10,"is_required":true,"required_files":[],"required_folders":[]}"""),

        // ── Phase 4b: sổ điểm, cột điểm, điểm cộng, thư viện lý do, các bộ mẫu, xuất file ─
        ("GET",    "/api/offerings/{offeringId}/gradebook", null),
        ("POST",   "/api/offerings/{offeringId}/gradebook/init", """{"template_id":null}"""),
        ("PUT",    "/api/offerings/{offeringId}/grade-columns",
            """{"columns":[{"id":null,"key":"cot_cua_b","name":"Cot do B dat","weight_percent":100,"order_index":0,"kind":"manual","n":0,"treat_missing_as_zero":false}]}"""),
        ("PUT",    "/api/grade-columns/{gradeColumnId}/sources", """{"sources":[]}"""),
        ("PUT",    "/api/grade-columns/{gradeColumnId}/grades",
            """{"enrollment_id":"{enrollmentId}","score":10,"note":"B ghi diem vao lop cua A"}"""),
        ("PUT",    "/api/offerings/{offeringId}/gradebook/settings",
            """{"bonus_default_target":"final_total","bonus_default_column_id":null,"bonus_max_points_per_student":1,"conditions":[]}"""),
        ("GET",    "/api/offerings/{offeringId}/gradebook/export", null),
        // Bản chốt sổ điểm (ADR-006): dữ liệu bất biến chứa TOÀN BỘ bảng điểm — MSSV, họ tên,
        // từng ô điểm của cả lớp. Lọt một id sang giảng viên khác là lộ nguyên một học kỳ.
        ("POST",   "/api/offerings/{offeringId}/gradebook/snapshot", null),
        ("GET",    "/api/offerings/{offeringId}/gradebook/snapshots", null),
        ("GET",    "/api/gradebook/snapshots/{snapshotId}", null),
        ("GET",    "/api/offerings/{offeringId}/bonus-points", null),
        ("POST",   "/api/bonus-points",
            """{"enrollment_id":"{enrollmentId}","target":"final_total","target_column_id":null,"points":1,"reason":"B cong diem cho sinh vien cua A","template_id":null}"""),
        ("DELETE", "/api/bonus-points/{bonusPointId}", null),
        // Sửa mẫu của người khác: id nằm trong BODY chứ không trong URL — chỗ dễ quên nhất,
        // vì nhìn route thì endpoint này trông như "tạo mới của chính mình".
        ("POST",   "/api/instructor/bonus-reasons",
            """{"id":"{bonusReasonId}","label":"B sua ly do cua A","default_points":1,"default_target":"final_total","default_column_key":null}"""),
        ("PUT",    "/api/instructor/bonus-reasons/{bonusReasonId}/visibility?is_active=false", null),
        ("POST",   "/api/instructor/grade-scheme-templates",
            """{"id":"{schemeTemplateId}","name":"B sua bo cot cua A","is_default":false,"items":[{"key":"thuong_ky","name":"Thuong ky","weight_percent":100,"order_index":0,"kind":"manual","n":0,"treat_missing_as_zero":false}]}"""),
        ("POST",   "/api/instructor/grade-export-templates",
            """{"id":"{exportTemplateId}","name":"B sua mau xuat cua A","is_default":false,"items":[{"source":"student_code","column_key":null,"header":"MSSV","order_index":0,"decimal_places":0}]}"""),

        // ── Phase 5: ngân hàng câu hỏi và đề trắc nghiệm ────────────────────────────────
        ("GET",    "/api/quizzes?subject_version_id={offeringVersionId}", null),
        ("GET",    "/api/quizzes/{quizId}", null),
        ("POST",   "/api/quizzes",
            """{"id":null,"subject_version_id":"{offeringVersionId}","offering_id":null,"title":"B soan de trong mon cua A","description":null,"config":{"is_graded":true,"time_limit_minutes":null,"max_attempts":null,"shuffle_questions":false,"shuffle_options":false,"show_explanation_after_submit":true}}"""),
        ("PUT",    "/api/quizzes/{quizId}/questions", """{"question_ids":[]}"""),
        ("POST",   "/api/questions",
            """{"id":"{questionId}","type":"single_choice","text":"B sua cau hoi cua A","explanation":null,"foundation_tags":[],"points":1,"allow_partial_credit":false,"options":[{"text":"Dung","is_correct":true},{"text":"Sai","is_correct":false}],"fill_blank":null}"""),
        ("DELETE", "/api/questions/{questionId}", null),

        // ── Phase 6: liêm chính học thuật và chấm nâng cao ──────────────────────────────
        // Thiếu offering_id thì model binder trả 400 và dòng test không kiểm được gì.
        ("GET",    "/api/assignments/{assignmentId}/similarity?offering_id={offeringId}", null),
        ("GET",    "/api/submissions/{submissionId}/compare/{submissionId2}", null),
        ("GET",    "/api/assignments/{assignmentId}/hint-unlocks?offering_id={offeringId}", null),
        ("POST",   "/api/submissions/{submissionId}/apply-penalties", null),
        ("GET",    "/api/submissions/{submissionId}/member-scores", null),
        ("PUT",    "/api/submissions/{submissionId}/member-scores", """{"scores":[]}"""),

        // ── Phase 7: quy tắc XP, cấu hình động lực, bảng xếp hạng, huy hiệu ─────────────
        ("GET",    "/api/offerings/{offeringId}/xp-rules", null),
        ("POST",   "/api/offerings/{offeringId}/xp-rules",
            """{"id":null,"event_type":"lesson_completed","points":10,"min_value":null,"daily_cap":null,"is_enabled":true}"""),
        ("POST",   "/api/offerings/{offeringId}/xp-rules/seed", null),
        ("DELETE", "/api/xp-rules/{xpRuleId}", null),
        ("GET",    "/api/offerings/{offeringId}/gamification-settings", null),
        ("PUT",    "/api/offerings/{offeringId}/gamification-settings",
            """{"is_enabled":true,"leaderboard_visibility":"real_names","leaderboard_size":10}"""),
        ("POST",   "/api/offerings/{offeringId}/gamification/recalculate", null),
        // Bảng xếp hạng phía giảng viên luôn hiện TÊN THẬT — lọt sang giảng viên khác là lộ
        // thẳng danh sách sinh viên kèm thành tích của cả một lớp.
        ("GET",    "/api/offerings/{offeringId}/gamification/leaderboard", null),
        ("POST",   "/api/badges",
            """{"id":"{badgeId}","code":"b_sua_huy_hieu_cua_a","name":"B sua huy hieu cua A","description":null,"icon":null,"logic":"all","is_enabled":true,"conditions":[{"metric":"total_xp","comparison":"at_least","threshold":100}]}"""),
        ("DELETE", "/api/badges/{badgeId}", null),
    ];

    /// <summary>Endpoint trả danh sách — không được lọt một phần tử nào của giảng viên khác.</summary>
    internal static readonly string[] DanhSach =
    [
        "/api/instructor/invites",
        "/api/subjects",
        "/api/offerings",

        // Tài nguyên thuộc GIẢNG VIÊN chứ không thuộc lớp: soạn một lần dùng lại cho mọi môn,
        // mọi học kỳ. Không có id nào trong URL nên bộ lọc instructor_id trong truy vấn là
        // thứ DUY NHẤT ngăn danh sách của người khác hiện ra — quên nó thì không có exception,
        // không có log lạ, chỉ là rubric và ngân hàng câu hỏi của đồng nghiệp nằm sẵn trong
        // danh sách của mình.
        "/api/rubrics",
        "/api/questions",
        "/api/badges",
        "/api/instructor/bonus-reasons",
        "/api/instructor/grade-scheme-templates",
        "/api/instructor/grade-export-templates",
    ];

    public static TheoryData<string, string, string?> TaiNguyenCuaGiangVienA()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var (method, template, body) in TaiNguyen) data.Add(method, template, body);
        return data;
    }

    /// <summary>Dựng request kèm body JSON nếu có — dùng chung cho cả hai test của ma trận.</summary>
    private static HttpRequestMessage TaoRequest(string method, string url, string? body)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return req;
    }

    public static TheoryData<string> EndpointDanhSach()
    {
        var data = new TheoryData<string>();
        foreach (var url in DanhSach) data.Add(url);
        return data;
    }

    [Theory]
    [MemberData(nameof(TaiNguyenCuaGiangVienA))]
    public async Task Giang_vien_khac_luon_nhan_404(string method, string template, string? body)
    {
        var url = fx.Resolve(template);

        var res = await fx.GvB.SendAsync(TaoRequest(method, url, body is null ? null : fx.ResolveBody(body)));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            $"{method} {url}: phải trả 404 cho giảng viên khác. Trả 403 là xác nhận id này CÓ TỒN TẠI, " +
            "đủ để dò ra cấu trúc dữ liệu của người khác chỉ bằng cách thử id. Trả 200 là đã rò rỉ thật. " +
            $"Trả 400 nghĩa là request chết ở validator trước khi tới lớp phân quyền — sửa body, đừng sửa kỳ vọng. " +
            $"Body đã gửi: {body ?? "(không có)"}");
    }

    [Theory]
    [MemberData(nameof(EndpointDanhSach))]
    public async Task Danh_sach_khong_lot_du_lieu_giang_vien_khac(string url)
    {
        var json = await fx.GvB.GetFromJsonAsync<JsonElement>(url);

        json.GetArrayLength().ShouldBe(0,
            $"{url}: giảng viên B chưa tạo gì cả nên danh sách phải rỗng. Có phần tử nghĩa là " +
            "câu truy vấn thiếu điều kiện lọc instructor_id.");
    }

    [Fact]
    public async Task Chinh_chu_van_truy_cap_duoc_tai_nguyen_cua_minh()
    {
        // Nếu thiếu test này thì một handler trả 404 cho TẤT CẢ mọi người cũng làm
        // ma trận trên xanh — tức là ma trận không chứng minh được gì về việc lọc đúng.
        // Cùng lý do với từng endpoint danh sách: "B thấy 0 phần tử" chỉ có nghĩa khi
        // "A thấy nhiều hơn 0" trên đúng endpoint đó.
        foreach (var url in DanhSach)
        {
            var ds = await fx.GvA.GetFromJsonAsync<JsonElement>(url);

            ds.GetArrayLength().ShouldBeGreaterThan(0,
                $"{url}: giảng viên A đã seed tài nguyên loại này nên danh sách không được rỗng. " +
                "Rỗng ở đây nghĩa là dòng tương ứng trong ma trận xanh vì endpoint trả rỗng cho " +
                "mọi người, không phải vì bộ lọc instructor_id chạy đúng.");
        }
    }

    [Fact]
    public async Task Moi_dong_GET_trong_ma_tran_that_su_tro_toi_du_lieu_cua_A()
    {
        // ĐỐI CHỨNG DƯƠNG cho toàn bộ ma trận. Một dòng có route gõ sai sẽ trả 404 từ tầng
        // ĐỊNH TUYẾN cho MỌI người — test "giảng viên B nhận 404" khi đó xanh mà không kiểm
        // được gì cả, và nó sẽ xanh mãi mãi kể cả khi endpoint thật đang rò rỉ dữ liệu.
        // Ở đây chính chủ A bắn vào đúng những URL đó: A mà cũng nhận 404 nghĩa là dòng ma
        // trận tương ứng đang xanh vì lý do sai.
        //
        // Chỉ lấy GET: chúng không đổi dữ liệu nên chạy chung fixture với các test khác được.
        // Các dòng POST/PUT/DELETE không thể đối chứng kiểu này — chạy chúng dưới danh nghĩa A
        // là thật sự xóa slot, thật sự chấm điểm, và fixture dùng chung sẽ hỏng cho test sau.
        foreach (var (method, template, _) in TaiNguyen.Where(x => x.Method == "GET"))
        {
            // Dòng đọc nội dung bên trong tệp nén là ngoại lệ: fixture ghi bản ghi tệp thẳng
            // vào database mà KHÔNG đặt một tệp ZIP thật trong storage, nên với A nó chết ở
            // tầng đọc file chứ không trả về nội dung. Với B thì vẫn dừng ở phép kiểm quyền
            // trước đó — đúng thứ dòng ma trận cần kiểm.
            if (template.Contains("/entry?", StringComparison.Ordinal)) continue;

            var url = fx.Resolve(template);
            var res = await fx.GvA.GetAsync(url);

            res.StatusCode.ShouldNotBe(HttpStatusCode.NotFound,
                $"{method} {url}: chính chủ A cũng nhận 404. Route sai hoặc tài nguyên chưa được " +
                "seed trong IsolationFixture — dòng ma trận tương ứng đang xanh vì lý do sai.");
        }
    }

    [Fact]
    public async Task Dashboard_khong_dem_lop_va_bai_nop_cua_giang_vien_khac()
    {
        // Trang chủ giảng viên không nhận id nào nên không có 404 để kiểm — cách duy nhất
        // canh nó là đối chiếu hai phía: A phải thấy lớp và bài chờ chấm của mình, B phải
        // thấy đúng số không. Thiếu vế của A thì một dashboard hỏng trả rỗng cho tất cả
        // cũng làm test này xanh.
        var b = await fx.GvB.GetFromJsonAsync<JsonElement>("/api/instructor/dashboard");

        b.GetProperty("so_lop_dang_day").GetInt32().ShouldBe(0);
        b.GetProperty("lop").GetArrayLength().ShouldBe(0);
        b.GetProperty("can_xu_ly").GetArrayLength().ShouldBe(0,
            "Danh sách 'cần xử lý' của B lọt bài nộp của lớp A là lộ tên sinh viên, tên bài " +
            "và thời điểm nộp của một lớp B không dạy.");

        var a = await fx.GvA.GetFromJsonAsync<JsonElement>("/api/instructor/dashboard");

        a.GetProperty("so_lop_dang_day").GetInt32().ShouldBeGreaterThan(0);
        a.GetProperty("can_xu_ly").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Tai_khoan_chi_co_vai_tro_tro_giang_bi_tu_choi_o_moi_endpoint_nghiep_vu()
    {
        // Canh gác cho ADR-013: vai trò TeachingAssistant tồn tại nhưng KHÔNG được cấp quyền gì.
        // Test này đỏ ngay nếu ai đó về sau vô tình thêm lại một policy cho nó.
        foreach (var (method, template, body) in TaiNguyen)
        {
            var url = fx.Resolve(template);
            var res = await fx.TroGiang.SendAsync(
                TaoRequest(method, url, body is null ? null : fx.ResolveBody(body)));

            res.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }

        foreach (var url in DanhSach)
        {
            var res = await fx.TroGiang.GetAsync(url);
            res.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }
    }
}
