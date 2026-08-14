using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Domain.Content;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class ContentControllerTests(TeachHubApiFactory factory)
{
    private static async Task<Guid> ThemChuongAsync(HttpClient gv, Guid versionId, string title = "Chương 1")
    {
        var res = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters", new { title });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ThemBaiAsync(HttpClient gv, Guid chapterId, string title)
    {
        var res = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> ThemAssignmentAsync(Guid versionId)
    {
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = new Assignment { SubjectVersionId = versionId, Title = "Bài tập" };
        db.Assignments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task Khong_them_duoc_chuong_vao_version_da_publish()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var res = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters",
            new { title = "Chương thêm lén" });

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Khong_xoa_duoc_chuong_cua_version_da_publish()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);
        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var res = await gv.DeleteAsync($"/api/chapters/{chapterId}");

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reorder_giu_order_index_lien_tuc_tu_0()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);

        await ThemBaiAsync(gv, chapterId, "Bài A");
        await ThemBaiAsync(gv, chapterId, "Bài B");
        var c = await ThemBaiAsync(gv, chapterId, "Bài C");

        (await gv.PutAsJsonAsync($"/api/lessons/{c}/reorder", new { to_index = 0 }))
            .EnsureSuccessStatusCode();

        var lessons = await gv.GetFromJsonAsync<JsonElement>($"/api/chapters/{chapterId}/lessons");
        lessons.EnumerateArray().Select(l => l.GetProperty("title").GetString())
            .ShouldBe(["Bài C", "Bài A", "Bài B"]);
        lessons.EnumerateArray().Select(l => l.GetProperty("order_index").GetInt32())
            .ShouldBe([0, 1, 2]);
    }

    [Fact]
    public async Task Chuong_co_hai_bai_mot_bai_co_component_bai_kia_khong()
    {
        // Kịch bản chính xác trong SRS mục 5.1 — nghiệm thu bằng test, không chỉ bằng lời.
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);
        var bai1 = await ThemBaiAsync(gv, chapterId, "Bài 1");
        var bai2 = await ThemBaiAsync(gv, chapterId, "Bài 2");
        var assignmentId = await ThemAssignmentAsync(versionId);

        (await gv.PostAsJsonAsync($"/api/lessons/{bai1}/components",
            new { type = "assignment", ref_id = assignmentId })).EnsureSuccessStatusCode();
        (await gv.PostAsJsonAsync($"/api/lessons/{bai1}/components",
            new { type = "reflection", config = new { reflection_prompt = "Em học được gì?" } }))
            .EnsureSuccessStatusCode();

        var l1 = await gv.GetFromJsonAsync<JsonElement>($"/api/lessons/{bai1}");
        var l2 = await gv.GetFromJsonAsync<JsonElement>($"/api/lessons/{bai2}");

        l1.GetProperty("components").GetArrayLength().ShouldBe(2);
        l2.GetProperty("components").GetArrayLength().ShouldBe(0);   // hoàn toàn hợp lệ
    }

    [Fact]
    public async Task Them_component_assignment_ma_thieu_ref_id_tra_ve_400()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);
        var lessonId = await ThemBaiAsync(gv, chapterId, "Bài 1");

        var res = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components",
            new { type = "assignment" });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("bắt buộc phải trỏ tới một bản ghi");
    }

    [Fact]
    public async Task Them_component_noi_dung_nhung_kem_ref_id_tra_ve_400()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);
        var lessonId = await ThemBaiAsync(gv, chapterId, "Bài 1");

        var res = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components",
            new { type = "reflection", ref_id = Guid.CreateVersion7() });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Xoa_component_giua_van_giu_order_index_lien_tuc()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);
        var lessonId = await ThemBaiAsync(gv, chapterId, "Bài 1");

        async Task<Guid> ThemAsync(string type)
        {
            var r = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components", new { type });
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        await ThemAsync("hook");
        var giua = await ThemAsync("reflection");
        await ThemAsync("custom_note");

        (await gv.DeleteAsync($"/api/lesson-components/{giua}")).EnsureSuccessStatusCode();

        var lesson = await gv.GetFromJsonAsync<JsonElement>($"/api/lessons/{lessonId}");
        lesson.GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("order_index").GetInt32()).ShouldBe([0, 1]);
        lesson.GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("type").GetString()).ShouldBe(["hook", "custom_note"]);
    }

    [Fact]
    public async Task Component_tro_toi_assignment_cua_version_khac_tra_ve_400()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, v1) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var (_, v2) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var chapterId = await ThemChuongAsync(gv, v1);
        var lessonId = await ThemBaiAsync(gv, chapterId, "Bài 1");
        var assignmentCuaV2 = await ThemAssignmentAsync(v2);

        var res = await gv.PostAsJsonAsync($"/api/lessons/{lessonId}/components",
            new { type = "assignment", ref_id = assignmentCuaV2 });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chuong_cua_giang_vien_khac_tra_ve_404_khong_phai_403()
    {
        var gv1 = await factory.LoginAsAsync("gv-noi-dung-1@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv-noi-dung-2@teachhub.local");

        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv1);
        var chapterId = await ThemChuongAsync(gv1, versionId);

        (await gv2.GetAsync($"/api/chapters/{chapterId}/lessons")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await gv2.DeleteAsync($"/api/chapters/{chapterId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Xoa_chuong_la_xoa_MEM_du_lieu_van_con_trong_bang()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        var chapterId = await ThemChuongAsync(gv, versionId);

        (await gv.DeleteAsync($"/api/chapters/{chapterId}")).EnsureSuccessStatusCode();

        var ds = await gv.GetFromJsonAsync<JsonElement>($"/api/subject-versions/{versionId}/chapters");
        ds.GetArrayLength().ShouldBe(0);

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Chapters.IgnoreQueryFilters().CountAsync(c => c.Id == chapterId)).ShouldBe(1);
    }
}
