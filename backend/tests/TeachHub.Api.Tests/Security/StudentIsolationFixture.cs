using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Review;
using TeachHub.Domain.Teaching;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests.Security;

/// <summary>Một "thế giới" hoàn chỉnh của một sinh viên: lớp riêng, dữ liệu học tập riêng.</summary>
public sealed class TheGioiSinhVien
{
    /// <summary>Nhãn để đọc thông báo lỗi cho dễ ("a" / "b").</summary>
    public required string Nhan { get; init; }

    /// <summary>Client đã đăng nhập của sinh viên CHÍNH trong thế giới này.</summary>
    public required HttpClient Sv { get; init; }

    /// <summary>Mã tham gia lớp — bí mật dùng chung, không phải id.</summary>
    public required string MaLop { get; init; }

    /// <summary>Mọi id thuộc về thế giới này, để thay vào template URL và để dò rò rỉ.</summary>
    public required IReadOnlyDictionary<string, string> Id { get; init; }
}

/// <summary>
/// Dựng HAI thế giới sinh viên hoàn toàn tách biệt để canh hướng SINH VIÊN ↔ SINH VIÊN.
///
/// Khác hẳn <see cref="IsolationFixture"/> (giảng viên ↔ giảng viên): ở đó kẻ tấn công bị chặn
/// ngay tại lớp policy vì sai VAI TRÒ, nên handler thậm chí không chạy. Ở đây hai client CÙNG
/// vai <c>Student</c>, policy cho qua cả hai, và thứ duy nhất còn lại đứng giữa dữ liệu của
/// người này với người kia là điều kiện lọc <c>enrollment_id</c>/<c>user_id</c> mà mỗi handler
/// phải TỰ viết. Không có gì canh việc đó ngoài file này.
///
/// Ba quyết định thiết kế, mỗi cái đều đã tính:
///
/// 1. Hai thế giới dùng CHUNG một giảng viên. Cố ý: nếu mỗi lớp một giảng viên thì một bộ lọc
///    <c>instructor_id</c> nằm đâu đó có thể vô tình chặn giúp, và ma trận sẽ xanh mà không
///    chứng minh được rằng handler có lọc theo người học hay không.
///
/// 2. Hai thế giới nằm trên HAI MÔN khác nhau, tức hai <c>subject_version</c> khác nhau.
///    Bắt buộc: <c>LearningAccess</c> và <c>SubmissionAccess</c> nối bài học/bài tập sang lớp
///    qua <c>SubjectVersionId</c>, nên hai lớp ghim CÙNG một phiên bản thì sinh viên lớp kia
///    có quyền hợp lệ với nội dung đó — dòng test sẽ đỏ vì thiết kế fixture sai, không vì lỗ hổng.
///
/// 3. Thế giới đối xứng: cả hai bên đều có đủ dữ liệu thật. Nhờ vậy mỗi dòng ma trận chạy được
///    HAI lần — một lần với id của người kia (phải bị chặn), một lần với id của chính mình
///    (phải đi lọt). Không có vế thứ hai thì một route gõ sai trả 404 cho tất cả mọi người
///    cũng làm ma trận xanh, và nó sẽ xanh mãi mãi.
///
/// Bài trắc nghiệm CỐ Ý không dùng chung câu hỏi với hàng đợi ôn tập: nộp một lượt quiz tính
/// điểm sẽ gọi <c>GenerateReviewItemsCommand</c>, và lệnh đó <c>ApplyAnswer(dung: false)</c> lên
/// mọi mục ôn tập đã có của cùng câu hỏi — tức đẩy <c>next_review_at</c> ra 3 ngày sau. Dòng
/// <c>/api/review/today</c> khi đó xanh hay đỏ tuỳ thứ tự xUnit chạy các dòng ma trận.
/// </summary>
public sealed class StudentIsolationFixture : TeachHubApiFactory
{
    public TheGioiSinhVien A { get; private set; } = null!;
    public TheGioiSinhVien B { get; private set; } = null!;

    private const string MatKhauSv = "Sinh@123";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var gv = await LoginAsInstructorAsync();

        A = await DungTheGioiAsync(gv, "a");
        B = await DungTheGioiAsync(gv, "b");
    }

    // ── Dựng một thế giới ──────────────────────────────────────────────────────────────

    private async Task<TheGioiSinhVien> DungTheGioiAsync(HttpClient gv, string nhan)
    {
        var id = new Dictionary<string, string>(StringComparer.Ordinal);

        // ---- Nội dung: môn → phiên bản nháp → 2 chương → bài → 3 component ----
        var (subjectId, versionId) = await SeedSubjectWithDraftAsync(gv);
        id["subjectId"] = subjectId.ToString();
        id["versionId"] = versionId.ToString();

        // Chương 1 mang nhãn nền tảng; component recap ở chương 2 sẽ lấy câu hỏi theo nhãn này.
        // Nhãn khác nhau giữa hai thế giới vì ngân hàng câu hỏi thuộc giảng viên và hai thế
        // giới dùng chung giảng viên — cùng nhãn thì recap của A trả cả câu của B.
        var nhanNenTang = $"on-tap-{nhan}";
        await TaoAsync(gv, $"/api/subject-versions/{versionId}/chapters", new
        {
            title = $"Chương 1 của {nhan}",
            objective = (string?)null,
            foundation_tags = new[] { nhanNenTang },
            unlock_rule = "free",
        });

        var chuong2 = await TaoAsync(gv, $"/api/subject-versions/{versionId}/chapters", new
        {
            title = $"Chương 2 của {nhan}",
            objective = (string?)null,
            foundation_tags = Array.Empty<string>(),
            unlock_rule = "free",
        });
        var chapterId = chuong2.GetProperty("id").GetGuid();
        id["chapterId"] = chapterId.ToString();

        var bai = await TaoAsync(gv, $"/api/chapters/{chapterId}/lessons", new { title = $"Bài của {nhan}" });
        var lessonId = bai.GetProperty("id").GetGuid();
        id["lessonId"] = lessonId.ToString();

        var hook = await TaoAsync(gv, $"/api/lessons/{lessonId}/components", new
        {
            type = "hook",
            ref_id = (Guid?)null,
            config = new { html = $"<p>Mở bài của {nhan}</p>" },
            is_required = false,
        });
        id["componentId"] = hook.GetProperty("id").GetGuid().ToString();

        var recap = await TaoAsync(gv, $"/api/lessons/{lessonId}/components", new
        {
            type = "recap",
            ref_id = (Guid?)null,
            config = new { recap_question_count = 3, recap_foundation_tags = new[] { nhanNenTang } },
            is_required = false,
        });
        id["recapComponentId"] = recap.GetProperty("id").GetGuid().ToString();

        var checkpoint = await TaoAsync(gv, $"/api/lessons/{lessonId}/components", new
        {
            type = "checkpoint_quiz",
            ref_id = (Guid?)null,
            config = new
            {
                inline_questions = new[]
                {
                    new
                    {
                        text = $"Câu hỏi nhúng của {nhan}?",
                        options = new[] { "Đúng", "Sai" },
                        correct_indexes = new[] { 0 },
                        explanation = (string?)null,
                    },
                },
            },
            is_required = false,
        });
        id["checkpointComponentId"] = checkpoint.GetProperty("id").GetGuid().ToString();

        // ---- Xuất bản rồi mở lớp. Bài tập và đề trắc nghiệm chỉ dựng được sau bước này. ----
        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var lop = await TaoAsync(gv, "/api/offerings", new
        {
            subject_id = subjectId,
            subject_version_id = versionId,
            semester = "HK1 2026-2027",
            class_code = $"CL{Guid.CreateVersion7().ToString("N")[^5..].ToUpperInvariant()}",
        });
        var offeringId = lop.GetProperty("id").GetGuid();
        id["offeringId"] = offeringId.ToString();
        var maLop = lop.GetProperty("join_code").GetString()!;

        // ---- Hai sinh viên: người chính, và một người nữa để nhóm không rỗng ----
        var (sv, _, enrollmentId) = await TaoSinhVienAsync(offeringId);
        var (_, _, enrollmentBanId) = await TaoSinhVienAsync(offeringId);
        id["enrollmentId"] = enrollmentId.ToString();

        // Nhóm KHÔNG chứa sinh viên chính: nhờ vậy dòng POST .../join của đối chứng dương trả
        // 200 thật chứ không phải 409 "bạn đã ở trong một nhóm".
        var nhom = await TaoAsync(gv, $"/api/offerings/{offeringId}/groups", new
        {
            name = $"Nhóm của {nhan}",
            member_enrollment_ids = new[] { enrollmentBanId },
        });
        id["groupId"] = nhom.GetProperty("id").GetGuid().ToString();

        // Đợt chấm cuối môn: endpoint đọc danh sách CỐ Ý không mang policy vai trò vì sinh viên
        // cũng cần xem đợt cuối môn của lớp mình. Không seed thì danh sách trả rỗng và đối chứng
        // dương của dòng tương ứng không phân biệt được "lọc đúng" với "trả rỗng cho tất cả".
        var dotCuoi = await TaoAsync(gv, $"/api/offerings/{offeringId}/final-assessments", new
        {
            type = "final_project",
            title = $"Đồ án cuối môn của {nhan}",
            description = (string?)null,
            grade_column_id = (Guid?)null,
            due_at = (DateTimeOffset?)null,
            submission_mode = "individual",
            max_score = 10m,
            use_suggested_slots = true,
        });
        id["finalAssessmentId"] = dotCuoi.GetProperty("id").GetGuid().ToString();

        // ---- Ngân hàng câu hỏi + đề trắc nghiệm ----
        var quizQuestionId = await TaoCauHoiAsync(gv, $"Câu trong đề của {nhan}", nhanNenTang);
        id["quizQuestionId"] = quizQuestionId.ToString();

        // Hai câu này KHÔNG nằm trong đề — xem chú thích ở đầu lớp về lý do tách.
        var onTap1 = await TaoCauHoiAsync(gv, $"Câu ôn tập 1 của {nhan}", nhanNenTang);
        var onTap2 = await TaoCauHoiAsync(gv, $"Câu ôn tập 2 của {nhan}", nhanNenTang);

        var quiz = await TaoAsync(gv, "/api/quizzes", new
        {
            id = (Guid?)null,
            subject_version_id = versionId,
            offering_id = (Guid?)null,
            title = $"Bài trắc nghiệm của {nhan}",
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
        var quizId = quiz.GetProperty("id").GetGuid();
        id["quizId"] = quizId.ToString();

        await ChayAsync(gv, HttpMethod.Put, $"/api/quizzes/{quizId}/questions",
            new { question_ids = new[] { quizQuestionId } });

        // ---- Bài tập + slot gợi ý (source_code nhận .zip, images nhận .png) ----
        var baiTap = await TaoAsync(gv, "/api/assignments", new
        {
            subject_version_id = versionId,
            title = $"Bài tập của {nhan}",
            description = "Dữ liệu seed cho ma trận cách ly sinh viên",
            due_at = (DateTimeOffset?)null,
            allow_late_submission = true,
            late_penalty_percent_per_day = 10m,
            max_score = 10m,
            use_suggested_slots = true,
        });
        var assignmentId = baiTap.GetProperty("id").GetGuid();
        id["assignmentId"] = assignmentId.ToString();
        id["slotId"] = baiTap.GetProperty("slots").EnumerateArray()
            .First().GetProperty("id").GetGuid().ToString();

        // ---- Gợi ý mở dần: chưa có endpoint nào tạo được, nên ghi thẳng vào DB ----
        var hintId = await TaoGoiYAsync(assignmentId);
        id["hintId"] = hintId.ToString();

        // ---- Động lực học tập: bật cơ chế và gieo bảng quy tắc XP ----
        await ChayAsync(gv, HttpMethod.Post, $"/api/offerings/{offeringId}/xp-rules/seed", null);
        await ChayAsync(gv, HttpMethod.Put, $"/api/offerings/{offeringId}/gamification-settings",
            new { is_enabled = true, leaderboard_visibility = "real_names", leaderboard_size = 10 });

        // ---- Hoạt động thật của sinh viên chính ----
        await ChayAsync(sv, HttpMethod.Post, $"/api/lessons/{lessonId}/complete?secondsSpent=120", null);
        await ChayAsync(sv, HttpMethod.Post, $"/api/lesson-components/{id["componentId"]}/complete", null);

        // Tham số query KHÔNG đi theo JsonNamingPolicy, nên gõ sai tên nó không sinh ra lỗi nào:
        // endpoint vẫn trả 200 và ghi 0 giây. Khẳng định con số ĐÃ VÀO tới database là cách duy
        // nhất biến một lần đổi tên tham số thành lỗi seed ồn ào thay vì dữ liệu âm thầm sai.
        await KiemDaGhiThoiGianHocAsync(enrollmentId, lessonId, 120);

        var (submissionId, zipFileId, imageFileId) = await NopBaiAsync(sv, assignmentId);
        id["submissionId"] = submissionId.ToString();
        id["submissionFileId"] = zipFileId.ToString();
        id["submissionFileXoaId"] = imageFileId.ToString();

        await ChayAsync(sv, HttpMethod.Post, $"/api/assignment-hints/{hintId}/unlock", null);

        // Lượt 1 nộp luôn để có lịch sử làm bài; lượt 2 để dở dang cho các dòng ma trận cần
        // một lượt ĐANG LÀM (lưu đáp án, nộp). Không giới hạn số lượt nên mở lượt 2 hợp lệ.
        var luot1 = await TaoAsync(sv, $"/api/quizzes/{quizId}/attempts", new { });
        await ChayAsync(sv, HttpMethod.Post,
            $"/api/quiz-attempts/{luot1.GetProperty("id").GetGuid()}/submit", null);

        var luot2 = await TaoAsync(sv, $"/api/quizzes/{quizId}/attempts", new { });
        id["attemptId"] = luot2.GetProperty("id").GetGuid().ToString();

        // ---- Hàng đợi ôn tập: hai mục ĐẾN HẠN, ghi thẳng vào DB ----
        // Mục 1 dành cho dòng "trả lời"; mục 2 không ai đụng tới, để dòng /review/today luôn
        // có ít nhất một mục thật mà khẳng định — trả lời một mục sẽ đẩy nó ra khỏi danh sách.
        var userId = await UserIdCuaAsync(enrollmentId);
        id["reviewItemId"] = (await TaoMucOnTapAsync(userId, onTap1)).ToString();
        id["reviewItemBenId"] = (await TaoMucOnTapAsync(userId, onTap2)).ToString();

        await ChayAsync(sv, HttpMethod.Post, $"/api/offerings/{offeringId}/my-gamification/sync", null);

        return new TheGioiSinhVien { Nhan = nhan, Sv = sv, MaLop = maLop, Id = id };
    }

    // ── Hạ tầng seed ───────────────────────────────────────────────────────────────────

    private async Task<Guid> TaoCauHoiAsync(HttpClient gv, string noiDung, string nhanNenTang)
    {
        var cau = await TaoAsync(gv, "/api/questions", new
        {
            id = (Guid?)null,
            type = "single_choice",
            text = noiDung,
            explanation = (string?)null,
            foundation_tags = new[] { nhanNenTang },
            points = 1m,
            allow_partial_credit = false,
            options = new[]
            {
                new { text = "Đúng", is_correct = true },
                new { text = "Sai", is_correct = false },
            },
            fill_blank = (object?)null,
        });

        return cau.GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid UserId, Guid EnrollmentId)> TaoSinhVienAsync(Guid offeringId)
    {
        // Sáu ký tự CUỐI: UUIDv7 xếp theo thời gian nên tiền tố gần như trùng nhau, và cả
        // assembly test dùng chung một database — email trùng làm test đỏ theo thứ tự chạy.
        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var email = $"sv-cachly-{duoi}@sv.edu.vn";

        Guid userId;
        Guid enrollmentId;

        using (var scope = CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

            if (!await roles.RoleExistsAsync(Roles.Student))
                await roles.CreateAsync(new AppRole(Roles.Student));

            var u = new AppUser
            {
                UserName = email, Email = email, FullName = $"SV {duoi}",
                StudentCode = $"22580{duoi}", EmailConfirmed = true,
            };

            var tao = await users.CreateAsync(u, MatKhauSv);
            if (!tao.Succeeded)
                throw new InvalidOperationException(
                    $"Seed thất bại: không tạo được tài khoản {email} — " +
                    string.Join("; ", tao.Errors.Select(e => e.Description)));

            await users.AddToRoleAsync(u, Roles.Student);

            var ghiDanh = new Enrollment
            {
                OfferingId = offeringId, StudentCode = u.StudentCode!, FullName = u.FullName,
                Email = email, UserId = u.Id, Status = EnrollmentStatus.Active,
            };

            db.Enrollments.Add(ghiDanh);
            await db.SaveChangesAsync();

            userId = u.Id;
            enrollmentId = ghiDanh.Id;
        }

        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = MatKhauSv });
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());

        return (client, userId, enrollmentId);
    }

    /// <summary>
    /// Tệp ZIP thật, có <c>README.md</c> và thư mục <c>src/</c> đúng như slot gợi ý đòi hỏi.
    /// Phải là ZIP thật: <c>FileSignatureValidator</c> đọc 8 byte đầu và từ chối tệp đổi đuôi,
    /// nên một mảng byte bịa ra sẽ làm bước upload trả 400 và cả thế giới không dựng được.
    /// </summary>
    private static byte[] TaoZip()
    {
        using var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("README.md").Open()))
                w.Write("# Bài nộp seed cho ma trận cách ly\n");

            using (var w = new StreamWriter(zip.CreateEntry("src/Program.cs").Open()))
                w.Write("// mã nguồn seed\n");
        }

        return ms.ToArray();
    }

    /// <summary>PNG 8×8 hợp lệ — chỉ cần chữ ký đúng để qua được bước kiểm định dạng.</summary>
    private static byte[] TaoPng()
    {
        using var anh = new Image<Rgba32>(8, 8);
        using var ms = new MemoryStream();
        anh.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Nộp qua ĐÚNG luồng multipart thật thay vì ghi thẳng bản ghi vào database. Cần vậy vì
    /// dòng <c>/entry?path=README.md</c> đọc nội dung tệp từ storage: một hàng trỏ vào tệp
    /// không tồn tại sẽ làm chính chủ cũng nhận lỗi, và đối chứng dương mất ý nghĩa.
    ///
    /// Hai tệp ở hai slot khác nhau là chủ ý: dòng DELETE trong ma trận thật sự xóa tệp, nên
    /// nó phải nhắm vào tệp ảnh — xóa mất tệp ZIP thì hai dòng đọc tệp nén phía trên sẽ nhận
    /// 404 tuỳ thứ tự xUnit chạy, tức đỏ vì thứ tự chứ không vì lỗ hổng.
    /// </summary>
    private static async Task<(Guid SubmissionId, Guid ZipFileId, Guid ImageFileId)> NopBaiAsync(
        HttpClient sv, Guid assignmentId)
    {
        await TaiLenAsync(sv, assignmentId, "source_code", "bai-nop.zip", "application/zip", TaoZip());
        await TaiLenAsync(sv, assignmentId, "images", "minh-chung.png", "image/png", TaoPng());

        var cua = await sv.GetFromJsonAsync<JsonElement>($"/api/assignments/{assignmentId}/my-submission");
        var hienHanh = cua.GetProperty("current");

        var files = hienHanh.GetProperty("files").EnumerateArray().ToList();

        return (
            hienHanh.GetProperty("id").GetGuid(),
            files.Single(f => f.GetProperty("slot_key").GetString() == "source_code")
                 .GetProperty("id").GetGuid(),
            files.Single(f => f.GetProperty("slot_key").GetString() == "images")
                 .GetProperty("id").GetGuid());
    }

    private static async Task TaiLenAsync(
        HttpClient sv, Guid assignmentId, string slotKey, string tenTep, string contentType, byte[] noiDung)
    {
        using var form = new MultipartFormDataContent();

        var tep = new ByteArrayContent(noiDung);
        tep.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(tep, "file", tenTep);

        // "slot_key" ghim tường minh: model binder của form KHÔNG theo JsonNamingPolicy.
        form.Add(new StringContent(slotKey), "slot_key");

        var res = await sv.PostAsync($"/api/assignments/{assignmentId}/submissions/draft", form);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: tải tệp {tenTep} lên slot {slotKey} trả {(int)res.StatusCode} — " +
                await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> TaoGoiYAsync(Guid assignmentId)
    {
        using var scope = CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var goiY = new AssignmentHint
        {
            AssignmentId = assignmentId,
            OrderIndex = 0,
            Content = "Gợi ý đầu tiên: đọc kỹ yêu cầu về cấu trúc thư mục.",
            PenaltyPercent = 10m,
        };

        db.AssignmentHints.Add(goiY);
        db.AssignmentHints.Add(new AssignmentHint
        {
            AssignmentId = assignmentId,
            OrderIndex = 1,
            Content = "Gợi ý thứ hai: bắt đầu từ hàm main.",
            PenaltyPercent = 20m,
        });

        await db.SaveChangesAsync();
        return goiY.Id;
    }

    /// <summary>
    /// Thời gian học đã thật sự vào bảng <c>lesson_progresses</c> chưa. Đây là cái bẫy mà chính
    /// endpoint này vừa dính: tên tham số query đổi từ <c>secondsSpent</c> sang
    /// <c>seconds_spent</c>, và mọi lời gọi dùng tên cũ vẫn nhận 200 — chỉ có con số là 0.
    /// </summary>
    private async Task KiemDaGhiThoiGianHocAsync(Guid enrollmentId, Guid lessonId, int mongDoi)
    {
        using var scope = CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var giay = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId && p.LessonId == lessonId)
            .Select(p => (int?)p.SecondsSpent)
            .SingleOrDefaultAsync();

        if (giay != mongDoi)
            throw new InvalidOperationException(
                $"Seed thất bại: đánh dấu học xong bài {lessonId} lẽ ra ghi {mongDoi} giây nhưng " +
                $"database có {giay?.ToString() ?? "(không có hàng nào)"}. Nhiều khả năng tên tham " +
                "số query của POST /api/lessons/{id}/complete đã đổi — model binder không đi theo " +
                "JsonNamingPolicy nên tên sai chỉ lặng lẽ nhận giá trị mặc định.");
    }

    private async Task<Guid> UserIdCuaAsync(Guid enrollmentId)
    {
        using var scope = CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Enrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => e.UserId!.Value)
            .SingleAsync();
    }

    /// <summary>
    /// Một mục ôn tập ĐÃ ĐẾN HẠN. Không đi qua luồng nộp quiz vì mục vừa sinh ra có
    /// <c>next_review_at</c> ở tương lai, nên nó KHÔNG hiện trong "Ôn tập hôm nay" và cũng
    /// không nhận được câu trả lời (409 "chưa tới hạn") — hai dòng ma trận sẽ mất hết ý nghĩa.
    /// </summary>
    private async Task<Guid> TaoMucOnTapAsync(Guid userId, Guid questionId)
    {
        using var scope = CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = TimeProvider.System.GetUtcNow();
        var muc = ReviewQueueItem.Create(userId, questionId, sourceQuizAttemptId: null, now);
        muc.NextReviewAt = now.AddDays(-1);

        db.ReviewQueueItems.Add(muc);
        await db.SaveChangesAsync();

        return muc.Id;
    }

    /// <summary>
    /// POST rồi trả về thân JSON. Không dùng <c>EnsureSuccessStatusCode</c> trần: seed hỏng vì
    /// một trường thiếu trong body chỉ hiện "400 Bad Request" và người sửa phải đoán trường nào.
    /// </summary>
    private static async Task<JsonElement> TaoAsync(HttpClient client, string url, object body)
    {
        var res = await client.PostAsJsonAsync(url, body);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: POST {url} trả {(int)res.StatusCode} — {await res.Content.ReadAsStringAsync()}");

        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Như <see cref="TaoAsync"/> nhưng cho endpoint không cần đọc thân phản hồi.</summary>
    private static async Task ChayAsync(HttpClient client, HttpMethod method, string url, object? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);

        var res = await client.SendAsync(req);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: {method} {url} trả {(int)res.StatusCode} — " +
                await res.Content.ReadAsStringAsync());
    }

    // ── Thay placeholder ───────────────────────────────────────────────────────────────

    /// <summary>Thay <c>{offeringId}</c>… trong template URL bằng id thật của thế giới đã cho.</summary>
    public static string Resolve(string template, TheGioiSinhVien tg)
    {
        var url = ThayId(template, tg);

        if (url.Contains('{', StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Template '{template}' còn placeholder chưa được seed trong StudentIsolationFixture: {url}");

        return url;
    }

    /// <summary>
    /// Thay placeholder trong THÂN request JSON. Không dùng lại <see cref="Resolve"/> được:
    /// thân JSON đầy dấu <c>{</c> của chính cú pháp JSON nên phép kiểm "còn ngoặc là còn
    /// placeholder" sẽ báo động ở mọi dòng.
    /// </summary>
    public static string ResolveBody(string body, TheGioiSinhVien tg)
    {
        var s = ThayId(body, tg);

        // Trong JSON, sau '{' luôn là '"', khoảng trắng, hoặc '}'. '{' đứng ngay trước một CHỮ
        // CÁI chỉ có thể là placeholder chưa seed — bắt tại đây thay vì để nó thành một lỗi 400
        // khó hiểu ở tầng đọc JSON, tức dòng ma trận đỏ vì lý do sai.
        for (var i = 0; i < s.Length - 1; i++)
            if (s[i] == '{' && char.IsLetter(s[i + 1]))
                throw new InvalidOperationException(
                    $"Thân request còn placeholder chưa được seed trong StudentIsolationFixture: {body}");

        return s;
    }

    private static string ThayId(string s, TheGioiSinhVien tg)
    {
        foreach (var (khoa, giaTri) in tg.Id)
            s = s.Replace($"{{{khoa}}}", giaTri, StringComparison.Ordinal);

        return s;
    }
}
