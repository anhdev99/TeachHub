using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class LessonFileAccessTests(TeachHubApiFactory factory)
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7\nnội dung slide"u8.ToArray()];

    /// <summary>Môn có 1 bài đã upload PDF, đã xuất bản, đã mở lớp và có 1 SV ghi danh.</summary>
    private async Task<(HttpClient Gv, HttpClient Sv, string FileUrl, Guid OfferingId)> SeedAsync()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters", new { title = "C1" });
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var l = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "B1" });
        var lessonId = (await l.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var form = new MultipartFormDataContent();
        var noiDung = new ByteArrayContent(Pdf);
        noiDung.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(noiDung, "file", "slide.pdf");
        var up = await gv.PostAsync($"/api/lessons/{lessonId}/file", form);
        up.EnsureSuccessStatusCode();
        var fileUrl = (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("file_url").GetString()!;

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);

        var duoi = Guid.CreateVersion7().ToString("N")[^6..];
        var email = $"xemfile-{duoi}@sv.edu.vn";
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            if (!await roles.RoleExistsAsync(Roles.Student)) await roles.CreateAsync(new AppRole(Roles.Student));

            var u = new AppUser
            {
                UserName = email, Email = email, FullName = "SV xem file",
                StudentCode = $"22560{duoi}", EmailConfirmed = true,
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

        return (gv, sv, fileUrl, offeringId);
    }

    [Fact]
    public async Task Giang_vien_so_huu_noi_dung_xem_duoc_file()
    {
        var (gv, _, fileUrl, _) = await SeedAsync();

        var res = await gv.GetAsync($"/api/files/{fileUrl}");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");

        // inline chứ không attachment: mục tiêu là xem trước trong trình duyệt.
        res.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        (await res.Content.ReadAsByteArrayAsync()).ShouldBe(Pdf);
    }

    [Fact]
    public async Task Sinh_vien_hoc_lop_do_xem_duoc_file()
    {
        var (_, sv, fileUrl, _) = await SeedAsync();

        var res = await sv.GetAsync($"/api/files/{fileUrl}");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Giang_vien_khac_KHONG_xem_duoc_file()
    {
        var (_, _, fileUrl, _) = await SeedAsync();
        var gvKhac = await factory.LoginAsAsync("gv-xem-file-khac@teachhub.local");

        var res = await gvKhac.GetAsync($"/api/files/{fileUrl}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sinh_vien_lop_khac_KHONG_xem_duoc_file()
    {
        var (_, _, fileUrl, _) = await SeedAsync();
        var (_, svKhac, _, _) = await SeedAsync();   // lớp hoàn toàn khác

        var res = await svKhac.GetAsync($"/api/files/{fileUrl}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Nguoi_chua_dang_nhap_KHONG_xem_duoc_file()
    {
        var (_, _, fileUrl, _) = await SeedAsync();

        var res = await factory.CreateClient().GetAsync($"/api/files/{fileUrl}");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duong_dan_khong_thuoc_bai_hoc_nao_tra_404_chu_khong_mo_file()
    {
        var (gv, _, _, _) = await SeedAsync();

        // Đường dẫn có thật về mặt định dạng nhưng không bài nào trỏ tới ⇒ không thuộc hệ thống.
        var res = await gv.GetAsync($"/api/files/lessons/{Guid.CreateVersion7()}.pdf");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("lessons/../../../etc/passwd")]
    public async Task Duong_dan_thoat_khoi_thu_muc_luu_tru_bi_tu_choi(string duongDanAc)
    {
        var (gv, _, _, _) = await SeedAsync();

        var res = await gv.GetAsync($"/api/files/{duongDanAc}");

        // 404 chứ không 403: đừng xác nhận rằng có gì đó ở ngoài kia để dò.
        res.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }
}
