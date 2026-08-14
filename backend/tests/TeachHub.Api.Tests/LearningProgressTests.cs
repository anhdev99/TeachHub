using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Content;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class LearningProgressTests(TeachHubApiFactory factory)
{
    /// <summary>
    /// Dựng: môn có 1 chương ở chế độ tuần tự, 2 bài, bài 1 bắt buộc; xuất bản; mở lớp;
    /// thêm 1 sinh viên vào danh sách và gắn tài khoản. Trả về client SV + id các thứ.
    /// </summary>
    private async Task<(HttpClient Sv, Guid OfferingId, Guid Bai1, Guid Bai2)> SeedLopDangHocAsync()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", unlock_rule = "after_previous_completed" });
        ch.EnsureSuccessStatusCode();
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b1 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons",
            new { title = "Bài 1 — Hooks", is_required_to_unlock_next = true });
        b1.EnsureSuccessStatusCode();
        var bai1 = (await b1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b2 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "Bài 2" });
        b2.EnsureSuccessStatusCode();
        var bai2 = (await b2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);

        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var email = $"hoc-{duoi}@sv.edu.vn";

        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            if (!await roles.RoleExistsAsync(Roles.Student))
                await roles.CreateAsync(new AppRole(Roles.Student));

            var u = new AppUser
            {
                UserName = email, Email = email, FullName = "Người học",
                StudentCode = $"22530{duoi}", EmailConfirmed = true,
            };
            await users.CreateAsync(u, "Sinh@123");
            await users.AddToRoleAsync(u, Roles.Student);

            db.Enrollments.Add(new Enrollment
            {
                OfferingId = offeringId, StudentCode = u.StudentCode!, FullName = u.FullName,
                Email = email, UserId = u.Id, Status = EnrollmentStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        var sv = factory.CreateClient();
        var dn = await sv.PostAsJsonAsync("/api/auth/login", new { email, password = "Sinh@123" });
        dn.EnsureSuccessStatusCode();
        sv.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await dn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString());

        return (sv, offeringId, bai1, bai2);
    }

    [Fact]
    public async Task Bai_2_bi_khoa_va_NEU_RO_bai_nao_dang_chan()
    {
        var (sv, offeringId, _, bai2) = await SeedLopDangHocAsync();

        var tienDo = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        var bai = tienDo.GetProperty("chapters")[0].GetProperty("lessons")
            .EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == bai2);

        bai.GetProperty("access").GetString().ShouldBe("locked");

        // Chỉ hiện ổ khóa mà không nói vì sao là nguồn tin nhắn hỏi bài số một của mọi LMS.
        bai.GetProperty("locked_reason").GetString()!.ShouldContain("Bài 1 — Hooks");
    }

    [Fact]
    public async Task Hoc_xong_bai_1_thi_bai_2_mo_va_phan_tram_cap_nhat()
    {
        var (sv, offeringId, bai1, bai2) = await SeedLopDangHocAsync();

        var truoc = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        truoc.GetProperty("overall_percent").GetDecimal().ShouldBe(0m);

        (await sv.PostAsync($"/api/lessons/{bai1}/complete", null)).EnsureSuccessStatusCode();

        var sau = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        var lessons = sau.GetProperty("chapters")[0].GetProperty("lessons").EnumerateArray().ToList();

        lessons.Single(l => l.GetProperty("id").GetGuid() == bai2)
               .GetProperty("access").GetString().ShouldBe("unlocked");
        lessons.Single(l => l.GetProperty("id").GetGuid() == bai1)
               .GetProperty("completed").GetBoolean().ShouldBeTrue();

        sau.GetProperty("overall_percent").GetDecimal().ShouldBe(50m);
        sau.GetProperty("chapters")[0].GetProperty("completion_percent").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task Bam_hoan_thanh_hai_lan_khong_gay_loi_500()
    {
        // Sinh viên bấm nhanh hai lần là chuyện thường; unique index sẽ chặn bản thứ hai
        // và handler phải nuốt đúng lỗi đó thay vì trả 500.
        var (sv, _, bai1, _) = await SeedLopDangHocAsync();

        var a = sv.PostAsync($"/api/lessons/{bai1}/complete", null);
        var b = sv.PostAsync($"/api/lessons/{bai1}/complete", null);
        var kq = await Task.WhenAll(a, b);

        kq.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sinh_vien_lop_khac_khong_danh_dau_duoc_bai_cua_lop_nay()
    {
        var (_, _, bai1, _) = await SeedLopDangHocAsync();
        var (svKhac, _, _, _) = await SeedLopDangHocAsync();   // lớp hoàn toàn khác

        var res = await svKhac.PostAsync($"/api/lessons/{bai1}/complete", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Giang_vien_khong_xem_duoc_my_progress_cua_lop_minh_day()
    {
        // my-progress là góc nhìn của NGƯỜI HỌC. Giảng viên không có enrollment nên không có
        // tiến độ — và endpoint này gắn Policies.Student.
        var (_, offeringId, _, _) = await SeedLopDangHocAsync();
        var gv = await factory.LoginAsInstructorAsync();

        var res = await gv.GetAsync($"/api/offerings/{offeringId}/my-progress");

        res.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Component_bat_buoc_chua_xong_thi_bai_ke_tiep_van_khoa()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương 1", unlock_rule = "after_previous_completed" });
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b1 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons",
            new { title = "Bài 1", is_required_to_unlock_next = true });
        var bai1 = (await b1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var b2 = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "Bài 2" });
        var bai2 = (await b2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cp = await gv.PostAsJsonAsync($"/api/lessons/{bai1}/components",
            new { type = "reflection", is_required = true });
        cp.EnsureSuccessStatusCode();
        var componentId = (await cp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);

        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var email = $"cp-{duoi}@sv.edu.vn";
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var u = new AppUser
            {
                UserName = email, Email = email, FullName = "SV component",
                StudentCode = $"22540{duoi}", EmailConfirmed = true,
            };
            await users.CreateAsync(u, "Sinh@123");
            await users.AddToRoleAsync(u, Roles.Student);
            db.Enrollments.Add(new Enrollment
            {
                OfferingId = offeringId, StudentCode = u.StudentCode!, FullName = u.FullName,
                Email = email, UserId = u.Id, Status = EnrollmentStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        var sv = factory.CreateClient();
        var dn = await sv.PostAsJsonAsync("/api/auth/login", new { email, password = "Sinh@123" });
        sv.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await dn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString());

        // Xem xong nội dung nhưng CHƯA làm phần bắt buộc
        (await sv.PostAsync($"/api/lessons/{bai1}/complete", null)).EnsureSuccessStatusCode();

        var giua = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        giua.GetProperty("chapters")[0].GetProperty("lessons").EnumerateArray()
            .Single(l => l.GetProperty("id").GetGuid() == bai2)
            .GetProperty("access").GetString().ShouldBe("locked");

        // Làm nốt phần bắt buộc thì mới mở
        (await sv.PostAsync($"/api/lesson-components/{componentId}/complete", null)).EnsureSuccessStatusCode();

        var sau = await sv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/my-progress");
        sau.GetProperty("chapters")[0].GetProperty("lessons").EnumerateArray()
            .Single(l => l.GetProperty("id").GetGuid() == bai2)
            .GetProperty("access").GetString().ShouldBe("unlocked");
    }
}
