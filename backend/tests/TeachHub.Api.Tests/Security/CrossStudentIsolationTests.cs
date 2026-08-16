using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests.Security;

/// <summary>Điều phải xảy ra khi một sinh viên bắn vào tài nguyên của sinh viên lớp khác.</summary>
public enum KyVong
{
    /// <summary>Endpoint nhận id trong URL ⇒ người ngoài phải nhận đúng 404. Không 403, không 200.</summary>
    KhongTimThay,

    /// <summary>
    /// Endpoint KHÔNG nhận id nào (danh sách "của tôi") ⇒ người ngoài vẫn nhận 200. Phép kiểm
    /// thật nằm ở THÂN phản hồi: không được lẫn một mẩu dữ liệu nào của người kia.
    /// </summary>
    HaiTramNhungKhongLanDuLieu,
}

/// <summary>
/// Ma trận cách ly SINH VIÊN ↔ SINH VIÊN.
///
/// <para>
/// Đây KHÔNG phải bản sao của <see cref="CrossInstructorIsolationTests"/>. Ma trận giảng viên
/// canh một mối đe dọa mà lớp policy đã chặn hộ một nửa: giảng viên B gõ vào endpoint sinh viên
/// thì nhận 403 TRƯỚC khi handler chạy, nên khoảng 25 endpoint <c>Policies.Student</c> nằm ngoài
/// tầm nó. Hướng sinh viên ↔ sinh viên không có lớp bảo vệ đó: hai client CÙNG vai <c>Student</c>,
/// policy cho qua cả hai, và thứ duy nhất còn lại là điều kiện lọc <c>enrollment_id</c> hoặc
/// <c>user_id</c> mà mỗi handler phải TỰ viết. Quên một chỗ thì không có exception, không có log
/// lạ — chỉ là bài nộp hoặc điểm của lớp khác hiện ra trên màn hình một sinh viên.
/// </para>
///
/// <para>
/// Khác biệt thứ hai, quan trọng hơn: <b>không phải dòng nào cũng kỳ vọng 404</b>. Endpoint kiểu
/// "danh sách của tôi" không nhận id nên người ngoài vẫn nhận <c>200</c> — và đó là hành vi ĐÚNG
/// khi thân rỗng. <c>200 []</c> và <c>200 [dữ liệu lớp khác]</c> đều là 200; chỉ khẳng định mã
/// trạng thái là để lọt đúng loại rò rỉ nguy hiểm nhất. Vì thế các dòng đó khẳng định cả NỘI DUNG.
/// </para>
///
/// <para>
/// Mỗi dòng chạy BỐN request, không phải một:
/// A→thế giới B và B→thế giới A (phải bị chặn), rồi A→thế giới A và B→thế giới B (phải đi lọt).
/// Vế thứ hai là bắt buộc ở MỌI dòng chứ không chỉ dòng GET — khác với ma trận giảng viên, nơi
/// fixture dùng chung khiến các dòng ghi không đối chứng được. Ở đây làm được vì hai thế giới
/// đối xứng: chính chủ tác động lên dữ liệu của CHÍNH MÌNH, không đụng vào bên kia. Thiếu vế đó
/// thì một route gõ sai trả 404 từ tầng ĐỊNH TUYẾN cho mọi người cũng làm ma trận xanh, và nó
/// sẽ xanh mãi mãi kể cả khi endpoint thật đang rò rỉ.
/// </para>
///
/// <para>
/// Placeholder trong URL và trong thân request đều được thay bằng <b>id THẬT của người kia</b>,
/// không phải GUID ngẫu nhiên. Gửi GUID ngẫu nhiên thì handler trả 404 chỉ vì "không có bản ghi
/// này" và dòng test xanh mà không chứng minh được gì; gửi đúng id mới ép handler trả lời câu
/// hỏi thật: "người gọi này có quyền trên tài nguyên đó không".
/// </para>
/// </summary>
[Collection(StudentIsolationCollection.Ten)]
public sealed class CrossStudentIsolationTests(StudentIsolationFixture fx)
{
    /// <summary>
    /// Mỗi dòng là một endpoint mà sinh viên gọi được. Phase nào thêm endpoint
    /// <c>Policies.Student</c> mới thì thêm dòng vào đây — cùng hạng mục Definition of Done với
    /// <see cref="CrossInstructorIsolationTests.TaiNguyen"/>, không phải việc tuỳ tâm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Body</c> phải là JSON đủ hợp lệ để đi qua <c>ValidationBehavior</c> — behavior đó chạy
    /// TRƯỚC handler, nên một trường sai luật trả 400 và dòng đó không bao giờ chạm tới lớp phân
    /// quyền. Method có thân mà gửi rỗng thì dừng ở 400 vì model binding, cũng vậy.
    /// </para>
    /// <para>
    /// KHÔNG có mặt ở đây, có lý do:
    /// <list type="bullet">
    /// <item><c>POST /api/assignments/{id}/submissions/draft</c> khai
    /// <c>[Consumes("multipart/form-data")]</c> nên thân JSON trả 415 trước khi tới handler —
    /// nó có <see cref="Sinh_vien_lop_khac_khong_tai_tep_len_bai_tap_cua_lop_khac"/> riêng, gửi
    /// multipart thật.</item>
    /// <item><c>POST /api/offerings/join</c> định danh bằng MÃ LỚP chứ không bằng id, và cố ý
    /// trả 403 cho mọi lý do thất bại — xem
    /// <see cref="Ma_lop_cua_lop_khac_khong_ghi_danh_duoc_va_khong_lo_ly_do"/>.</item>
    /// <item><c>GET /api/files/{**path}</c> định danh bằng ĐƯỜNG DẪN chứ không bằng id;
    /// hướng sinh viên ↔ sinh viên của nó đã có
    /// <c>LessonFileAccessTests.Sinh_vien_lop_khac_KHONG_xem_duoc_file</c> canh.</item>
    /// <item><c>DELETE /api/submission-files/{fileId}</c> PHÁ HỦY chính tài nguyên nó nhắm tới.
    /// Đối chứng dương của nó (chính chủ xóa tệp của mình) làm hàng biến mất, nên nếu xUnit
    /// chạy vế đó trước thì vế "người ngoài nhận 404" xanh vì hàng KHÔNG CÒN chứ không vì bị
    /// từ chối — đúng loại xanh giả mà cả ma trận này sinh ra để tránh. Nó cần thứ tự tường
    /// minh, xem <see cref="Sinh_vien_lop_khac_khong_xoa_duoc_tep_nop_cua_nguoi_kia"/>.</item>
    /// <item><c>/api/auth/me</c>, <c>/api/auth/change-password</c>, <c>/api/auth/logout</c>:
    /// chỉ đọc/ghi chính người đang đăng nhập, không nhận id của ai khác nên không có hướng
    /// tấn công chéo để canh.</item>
    /// </list>
    /// </para>
    /// </remarks>
    internal static readonly (string Method, string Template, string? Body, KyVong KyVong)[] MaTranSinhVien =
    [
        // ── Phase 1–2: nội dung bài học và tiến độ ──────────────────────────────────────
        ("GET",  "/api/offerings/{offeringId}/my-progress", null, KyVong.KhongTimThay),
        ("POST", "/api/lessons/{lessonId}/complete?seconds_spent=30", null, KyVong.KhongTimThay),
        ("POST", "/api/lesson-components/{componentId}/complete", null, KyVong.KhongTimThay),
        // Không nhận id: bộ lọc enrollment.user_id trong câu truy vấn là thứ DUY NHẤT ngăn lớp
        // của người khác hiện ra, và quên nó không sinh ra lỗi nào — chỉ là một dòng lớp lạ.
        ("GET",  "/api/me/offerings", null, KyVong.HaiTramNhungKhongLanDuLieu),

        // ── Phase 3: bài tập và bài nộp ─────────────────────────────────────────────────
        ("GET",  "/api/offerings/{offeringId}/assignments", null, KyVong.KhongTimThay),
        ("GET",  "/api/assignments/{assignmentId}/my-submission", null, KyVong.KhongTimThay),
        ("POST", "/api/assignments/{assignmentId}/submissions/submit", null, KyVong.KhongTimThay),
        ("GET",  "/api/submissions/{submissionId}", null, KyVong.KhongTimThay),
        ("GET",  "/api/submissions/{submissionId}/files/{submissionFileId}/file-tree", null, KyVong.KhongTimThay),
        // Đọc NỘI DUNG một tệp bên trong bài nộp của người khác — rò rỉ ở đây là đọc thẳng mã
        // nguồn bạn cùng khoá đã nộp, không phải chỉ tên tệp.
        ("GET",  "/api/submissions/{submissionId}/files/{submissionFileId}/entry?path=README.md", null, KyVong.KhongTimThay),

        // ── Phase 4a: nhóm và đợt cuối môn ─────────────────────────────────────────────
        ("GET",  "/api/offerings/{offeringId}/groups", null, KyVong.KhongTimThay),
        ("POST", "/api/groups/{groupId}/join", null, KyVong.KhongTimThay),
        // Endpoint này KHÔNG mang policy vai trò — cả giảng viên lẫn sinh viên cùng đọc, nên
        // <c>Policies.Student</c> không phải là tiêu chí để tìm ra nó. Chỉ có phép kiểm ghi danh
        // ngay trong handler đứng giữa đề cuối môn của lớp này với sinh viên lớp khác.
        ("GET",  "/api/offerings/{offeringId}/final-assessments", null, KyVong.KhongTimThay),

        // ── Phase 5: trắc nghiệm ───────────────────────────────────────────────────────
        ("GET",  "/api/offerings/{offeringId}/quizzes", null, KyVong.KhongTimThay),
        ("POST", "/api/quizzes/{quizId}/attempts", null, KyVong.KhongTimThay),
        ("GET",  "/api/quiz-attempts/{attemptId}", null, KyVong.KhongTimThay),
        ("PUT",  "/api/quiz-attempts/{attemptId}/answers", """{"answers":[]}""", KyVong.KhongTimThay),
        ("POST", "/api/quiz-attempts/{attemptId}/submit", null, KyVong.KhongTimThay),

        // ── Phase 6: gợi ý mở dần ──────────────────────────────────────────────────────
        // Nội dung gợi ý là thứ sinh viên phải TRẢ ĐIỂM mới xem được. Lọt sang lớp khác là vừa
        // lộ nội dung vừa vô hiệu hóa cả cơ chế trừ điểm.
        ("GET",  "/api/assignments/{assignmentId}/hints", null, KyVong.KhongTimThay),
        ("POST", "/api/assignment-hints/{hintId}/unlock", null, KyVong.KhongTimThay),

        // ── Phase 7: động lực học tập ──────────────────────────────────────────────────
        ("GET",  "/api/offerings/{offeringId}/my-gamification", null, KyVong.KhongTimThay),
        ("POST", "/api/offerings/{offeringId}/my-gamification/sync", null, KyVong.KhongTimThay),
        ("GET",  "/api/offerings/{offeringId}/leaderboard", null, KyVong.KhongTimThay),

        // ── Phase 8: ôn tập ngắt quãng ─────────────────────────────────────────────────
        // Hàng đợi ôn tập gắn với CON NGƯỜI chứ không với lớp, nên nó là nhóm endpoint sinh viên
        // duy nhất mà một bộ lọc theo lớp cũng không cứu được — chỉ có user_id đứng giữa.
        ("GET",  "/api/review/today", null, KyVong.HaiTramNhungKhongLanDuLieu),
        ("POST", "/api/review/items/{reviewItemId}/answer", """{"selected_option_ids":[],"text":null}""", KyVong.KhongTimThay),
        ("GET",  "/api/lesson-components/{recapComponentId}/recap", null, KyVong.KhongTimThay),

        // ── Chấm câu hỏi nhúng trong bài học ───────────────────────────────────────────
        ("POST", "/api/lesson-components/{checkpointComponentId}/check",
            """{"answers":[{"index":0,"selected_indexes":[0]}]}""", KyVong.KhongTimThay),
    ];

    public static TheoryData<string, string, string?, KyVong> DongMaTran()
    {
        var data = new TheoryData<string, string, string?, KyVong>();
        foreach (var (method, template, body, kyVong) in MaTranSinhVien)
            data.Add(method, template, body, kyVong);
        return data;
    }

    /// <summary>
    /// Cùng một mảng nguồn, bỏ cột kỳ vọng: đối chứng dương không phân biệt hai loại kỳ vọng vì
    /// chính chủ luôn phải đi lọt. Sinh từ <see cref="MaTranSinhVien"/> chứ không chép tay ra
    /// bảng thứ hai — hai bảng song song là hai bảng sẽ lệch nhau ở phase sau.
    /// </summary>
    public static TheoryData<string, string, string?> DongMaTranChinhChu()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var (method, template, body, _) in MaTranSinhVien)
            data.Add(method, template, body);
        return data;
    }

    private static HttpRequestMessage TaoRequest(string method, string url, string? body)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return req;
    }

    // ── Vế 1: người ngoài phải bị chặn ─────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(DongMaTran))]
    public async Task Sinh_vien_lop_khac_khong_cham_duoc_du_lieu_cua_nguoi_kia(
        string method, string template, string? body, KyVong kyVong)
    {
        // Chạy CẢ HAI chiều. Hai thế giới seed bằng cùng một đoạn mã nên về lý thuyết đối xứng,
        // nhưng một handler lọc nhầm theo thứ tự ghi danh hay theo id nhỏ hơn sẽ chỉ hở một
        // chiều — và chiều nào hở thì không đoán trước được.
        await NguoiNgoaiBiChanAsync(method, template, body, kyVong, keGoi: fx.B, nanNhan: fx.A);
        await NguoiNgoaiBiChanAsync(method, template, body, kyVong, keGoi: fx.A, nanNhan: fx.B);
    }

    private static async Task NguoiNgoaiBiChanAsync(
        string method, string template, string? body, KyVong kyVong,
        TheGioiSinhVien keGoi, TheGioiSinhVien nanNhan)
    {
        var url = StudentIsolationFixture.Resolve(template, nanNhan);

        using var req = TaoRequest(method, url,
            body is null ? null : StudentIsolationFixture.ResolveBody(body, nanNhan));

        var res = await keGoi.Sv.SendAsync(req);
        var than = await res.Content.ReadAsStringAsync();

        if (kyVong == KyVong.KhongTimThay)
        {
            res.StatusCode.ShouldBe(HttpStatusCode.NotFound,
                $"{method} {url}: sinh viên “{keGoi.Nhan}” không học lớp này nên phải nhận 404. " +
                "Trả 403 là xác nhận id đó CÓ TỒN TẠI — dò id tuần tự là đếm được bài nộp của cả " +
                "khoá. Trả 200 là đã rò rỉ thật. Trả 400 nghĩa là request chết ở validator trước " +
                $"khi tới lớp phân quyền: sửa body, đừng sửa kỳ vọng. Thân đã gửi: {body ?? "(không có)"}. " +
                $"Phản hồi: {than}");

            return;
        }

        // Endpoint không nhận id: 200 là ĐÚNG, nên mã trạng thái không kiểm được gì. Phép kiểm
        // thật là thân phản hồi — một danh sách rỗng và một danh sách đầy dữ liệu của người khác
        // trông giống hệt nhau nếu chỉ nhìn con số 200.
        res.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"{method} {url}: đây là endpoint “của tôi”, không nhận id nào, nên mọi sinh viên đã " +
            $"đăng nhập đều phải nhận 200. Phản hồi: {than}");

        KhongLanDuLieuCua(than, nanNhan, $"{method} {url} gọi bởi sinh viên “{keGoi.Nhan}”");
    }

    /// <summary>
    /// Không một id nào của thế giới kia được xuất hiện trong thân phản hồi. Kiểm trên chuỗi thô
    /// chứ không lần theo từng khoá DTO: rò rỉ hay nằm ở đúng những trường mà người viết test
    /// không nghĩ tới, và một khoá mới thêm ở phase sau sẽ không có ai kiểm.
    /// </summary>
    private static void KhongLanDuLieuCua(string than, TheGioiSinhVien nanNhan, string boiCanh)
    {
        foreach (var (khoa, giaTri) in nanNhan.Id)
            than.Contains(giaTri, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"{boiCanh}: thân phản hồi chứa {khoa} của thế giới “{nanNhan.Nhan}”. Endpoint này " +
                "trả 200 cho mọi người nên KHÔNG có mã trạng thái nào bắt được lỗi này — câu truy " +
                $"vấn đang thiếu điều kiện lọc theo người học. Phản hồi: {than}");
    }

    // ── Vế 2: chính chủ vẫn dùng được ──────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(DongMaTranChinhChu))]
    public async Task Chinh_chu_van_dung_duoc_dung_endpoint_do(
        string method, string template, string? body)
    {
        // ĐỐI CHỨNG DƯƠNG cho từng dòng, ở CẢ HAI thế giới. Không có nó thì một route gõ sai
        // (hoặc một id chưa được seed) trả 404 từ tầng ĐỊNH TUYẾN cho mọi người, và vế 1 phía
        // trên xanh vĩnh viễn mà không kiểm được gì cả.
        await ChinhChuDiLotAsync(method, template, body, fx.A);
        await ChinhChuDiLotAsync(method, template, body, fx.B);
    }

    private static async Task ChinhChuDiLotAsync(
        string method, string template, string? body, TheGioiSinhVien chinhChu)
    {
        var url = StudentIsolationFixture.Resolve(template, chinhChu);

        using var req = TaoRequest(method, url,
            body is null ? null : StudentIsolationFixture.ResolveBody(body, chinhChu));

        var res = await chinhChu.Sv.SendAsync(req);
        var than = await res.Content.ReadAsStringAsync();

        var lyDo =
            $"{method} {url}: chính chủ “{chinhChu.Nhan}” bắn vào tài nguyên CỦA MÌNH mà bị chặn. " +
            "Route sai, id chưa được seed trong StudentIsolationFixture, hoặc handler lọc quá tay — " +
            $"dòng ma trận tương ứng đang xanh vì lý do sai. Phản hồi: {than}";

        res.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, lyDo);
        res.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, lyDo);
        res.StatusCode.ShouldNotBe(HttpStatusCode.MethodNotAllowed, lyDo);

        if (method != "GET")
        {
            // Các dòng GHI không khẳng định được đúng 200: fixture dùng chung nên thứ tự xUnit
            // chạy các dòng quyết định trạng thái (nộp bài rồi thì xóa tệp trả 409, nộp quiz rồi
            // thì lưu đáp án trả 409). Allowlist hẹp này vẫn bắt được 401/415/500 — tức vẫn bắt
            // được đúng thứ mà đối chứng dương sinh ra để bắt: route sai hoặc thân request sai.
            res.StatusCode.ShouldBeOneOf(
                new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }, lyDo);

            return;
        }

        res.StatusCode.ShouldBe(HttpStatusCode.OK, lyDo);
        than.ShouldNotBeNullOrEmpty(lyDo);

        // Dữ liệu THẬT, không phải một phản hồi rỗng: ít nhất một id của chính thế giới này phải
        // có mặt. Endpoint trả rỗng cho tất cả mọi người cũng làm vế 1 xanh.
        //
        // Chỉ áp cho phản hồi JSON. Endpoint /entry trả về NỘI DUNG tệp (text/plain) — nội dung
        // một tệp sinh viên nộp thì đương nhiên không chứa id nào, và ép nó chứa id là buộc
        // fixture phải nhét GUID vào trong tệp ZIP để chiều lòng phép khẳng định.
        if (res.Content.Headers.ContentType?.MediaType != "application/json") return;

        chinhChu.Id.Values.Any(v => than.Contains(v, StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue(
                $"{method} {url}: chính chủ nhận 200 nhưng thân không chứa một id nào của thế giới " +
                $"“{chinhChu.Nhan}”. Endpoint đang trả rỗng cho mọi người, nên dòng ma trận tương " +
                $"ứng xanh mà không chứng minh được gì. Phản hồi: {than}");
    }

    // ── Các đường không đi qua ma trận ─────────────────────────────────────────────────

    [Fact]
    public async Task Sinh_vien_lop_khac_khong_tai_tep_len_bai_tap_cua_lop_khac()
    {
        // Vắng mặt trong ma trận vì endpoint khai [Consumes("multipart/form-data")]: gửi thân
        // JSON thì dừng ở 415 trước khi tới handler, tức dòng test đỏ vì content-type chứ không
        // vì phân quyền. Đây lại đúng là endpoint GHI nguy hiểm nhất phía sinh viên — nộp đè lên
        // không gian bài tập của lớp khác — nên nó phải được canh bằng multipart thật.
        using var form = new MultipartFormDataContent();

        var tep = new ByteArrayContent("day khong phai zip that"u8.ToArray());
        tep.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(tep, "file", "bai-nop.zip");
        form.Add(new StringContent("source_code"), "slot_key");

        var res = await fx.B.Sv.PostAsync(
            $"/api/assignments/{fx.A.Id["assignmentId"]}/submissions/draft", form);

        // 404 chứ không 403, và phải xảy ra TRƯỚC mọi phép kiểm nội dung tệp: thứ tự trong
        // UploadSubmissionFileHandler là kiểm ghi danh → kiểm slot → kiểm định dạng. Nhận 400 ở
        // đây nghĩa là handler đã đọc tới thân tệp của một bài tập không thuộc người gọi, tức
        // phép kiểm quyền nằm sai chỗ dù kết cục vẫn là từ chối.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "Sinh viên lớp khác tải tệp lên bài tập của lớp A phải nhận 404 ngay ở bước kiểm ghi " +
            $"danh. Phản hồi: {await res.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Sinh_vien_lop_khac_khong_xoa_duoc_tep_nop_cua_nguoi_kia()
    {
        // Dòng DUY NHẤT không đưa vào ma trận được vì nó PHÁ HỦY chính thứ nó nhắm tới. Trong
        // ma trận, đối chứng dương (chính chủ xóa tệp của mình) và vế người ngoài chạy theo thứ
        // tự xUnit tự chọn; chạy đối chứng dương trước là hàng biến mất, và "người ngoài nhận
        // 404" khi đó xanh vì KHÔNG CÒN HÀNG chứ không vì bị từ chối. Ở đây thứ tự do test đặt.
        var fileId = fx.A.Id["submissionFileXoaId"];

        (await TepConTonTaiAsync(fx.A, fileId)).ShouldBeTrue(
            "Tệp mồi chưa được seed đúng — phép kiểm bên dưới sẽ vô nghĩa.");

        var res = await fx.B.Sv.DeleteAsync($"/api/submission-files/{fileId}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "Sinh viên lớp khác xóa tệp trong bài nộp của người ta phải nhận 404, không phải 403 " +
            $"— 403 xác nhận tệp đó có thật. Phản hồi: {await res.Content.ReadAsStringAsync()}");

        // Phép khẳng định quan trọng nhất của cả test: 404 ở trên là "anh không có quyền", KHÔNG
        // phải "hàng này đã biến mất". Thiếu dòng này thì một handler xóa trước rồi mới kiểm
        // quyền vẫn xanh — và lúc đó tệp của người khác đã mất thật.
        (await TepConTonTaiAsync(fx.A, fileId)).ShouldBeTrue(
            "Request bị từ chối nhưng tệp vẫn biến mất: phép kiểm quyền đang chạy SAU thao tác xóa.");

        // Đối chứng dương: chính chủ vẫn thao tác được trên tệp của mình. 409 cũng đạt — bài có
        // thể đã nộp chính thức bởi một dòng ma trận khác, và khi đó "không xóa được nữa" là luật
        // nghiệp vụ đúng. Thứ phải loại trừ là 404: nó nghĩa là route sai hoặc id chưa seed.
        var chinhChu = await fx.A.Sv.DeleteAsync($"/api/submission-files/{fileId}");

        chinhChu.StatusCode.ShouldBeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "Chính chủ xóa tệp CỦA MÌNH mà nhận 404: route sai hoặc id chưa được seed, tức phép " +
            $"kiểm phía trên đang xanh vì lý do sai. Phản hồi: {await chinhChu.Content.ReadAsStringAsync()}");
    }

    /// <summary>Hàng tệp còn nằm trong bài nộp hiện hành của chính chủ hay không.</summary>
    private static async Task<bool> TepConTonTaiAsync(TheGioiSinhVien tg, string fileId)
    {
        var cua = await tg.Sv.GetFromJsonAsync<JsonElement>(
            $"/api/assignments/{tg.Id["assignmentId"]}/my-submission");

        var hienHanh = cua.GetProperty("current");

        return hienHanh.ValueKind == JsonValueKind.Object
            && hienHanh.GetProperty("files").EnumerateArray()
                .Any(f => string.Equals(f.GetProperty("id").GetString(), fileId,
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ma_lop_cua_lop_khac_khong_ghi_danh_duoc_va_khong_lo_ly_do()
    {
        // Ngoại lệ DUY NHẤT của luật "404 chứ không 403" trong file này, và là ngoại lệ có lý:
        // mã lớp là một BÍ MẬT DÙNG CHUNG chứ không phải id, nên "mã không tồn tại" và "mã đúng
        // nhưng bạn không có tên trong danh sách" đều trả 403 với MỘT thông điệp duy nhất. Phân
        // biệt hai ca đó là đưa cho người dò mã đúng tín hiệu họ cần để dò ra mã hợp lệ.
        var thatBai = await fx.B.Sv.PostAsJsonAsync("/api/offerings/join", new { join_code = fx.A.MaLop });
        var thanThatBai = await thatBai.Content.ReadAsStringAsync();

        thatBai.StatusCode.ShouldBe(HttpStatusCode.Forbidden, thanThatBai);

        var maBia = await fx.B.Sv.PostAsJsonAsync("/api/offerings/join", new { join_code = "KHONGCO" });
        var thanMaBia = await maBia.Content.ReadAsStringAsync();

        maBia.StatusCode.ShouldBe(HttpStatusCode.Forbidden, thanMaBia);

        // Cùng mã trạng thái CHƯA đủ — nội dung khác nhau cũng là một kênh phân biệt.
        using var docThatBai = JsonDocument.Parse(thanThatBai);
        using var docMaBia = JsonDocument.Parse(thanMaBia);

        docThatBai.RootElement.GetProperty("detail").GetString()
            .ShouldBe(docMaBia.RootElement.GetProperty("detail").GetString(),
                "Mã lớp có thật nhưng không có tên trong danh sách, và mã hoàn toàn bịa ra, phải " +
                "trả về CÙNG một thông điệp. Khác nhau là người dò mã phân biệt được hai ca đó và " +
                "dò ra mã hợp lệ chỉ bằng cách thử.");

        // Và không được ghi danh thật: lớp của A không được xuất hiện trong danh sách của B.
        var lopCuaB = await fx.B.Sv.GetStringAsync("/api/me/offerings");

        lopCuaB.Contains(fx.A.Id["offeringId"], StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
            "Mã lớp bị từ chối nhưng ghi danh vẫn được ghi — 403 lúc đó chỉ là một thông báo trên " +
            "màn hình chứ không phải một phép chặn.");
    }

    [Fact]
    public void Moi_dong_khai_dung_loai_ky_vong()
    {
        // Canh gác cho chính bảng. Một dòng CÓ id trong URL mà khai HaiTramNhungKhongLanDuLieu
        // sẽ tự phá phép kiểm rò rỉ: id vừa gửi lên gần như chắc chắn vọng lại trong thân phản
        // hồi, nên "không được chứa id của người kia" đỏ ngay dù handler hoàn toàn đúng.
        // Ngược lại, một dòng KHÔNG có id mà khai KhongTimThay sẽ đòi 404 ở một endpoint mà 200
        // mới là đúng — và người sửa sẽ đi tìm lỗi trong handler.
        foreach (var (method, template, _, kyVong) in MaTranSinhVien)
        {
            var coPlaceholder = template.Contains('{', StringComparison.Ordinal);

            (kyVong == KyVong.KhongTimThay).ShouldBe(coPlaceholder,
                $"{method} {template}: dòng có placeholder thì kỳ vọng phải là KhongTimThay, dòng " +
                "không có placeholder thì phải là HaiTramNhungKhongLanDuLieu.");
        }
    }

    [Fact]
    public void Hai_the_gioi_khong_dung_chung_mot_id_nao()
    {
        // Canh gác cho chính fixture. Nếu hai thế giới lỡ dùng chung một lớp học phần hay một
        // phiên bản môn thì mọi dòng "phải nhận 404" ở trên đỏ hàng loạt và người sửa sẽ đi tìm
        // lỗ hổng trong mã sản xuất — trong khi lỗi nằm ở dữ liệu seed.
        fx.A.Id.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .ShouldBe(fx.B.Id.Keys.OrderBy(k => k, StringComparer.Ordinal),
                "Hai thế giới phải seed ra cùng một bộ khoá, nếu không mỗi dòng ma trận chỉ chạy " +
                "được một chiều.");

        fx.A.Id.Values.Intersect(fx.B.Id.Values, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty(
            "Hai thế giới sinh viên phải hoàn toàn tách biệt. Dùng chung dù chỉ một id thì dòng ma " +
            "trận tương ứng kiểm nhầm: người gọi có quyền THẬT trên tài nguyên đó.");

        fx.A.MaLop.ShouldNotBe(fx.B.MaLop);
    }
}
