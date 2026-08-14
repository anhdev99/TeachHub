using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

public class SubjectsControllerTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    [Fact]
    public async Task Tao_mon_hoc_tu_dong_sinh_luon_mot_version_draft_dau_tien()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var versions = await gv.GetFromJsonAsync<JsonElement>($"/api/subjects/{subjectId}/versions");

        versions.GetArrayLength().ShouldBe(1);
        versions[0].GetProperty("id").GetGuid().ShouldBe(versionId);
        versions[0].GetProperty("status").GetString().ShouldBe("draft");
    }

    [Fact]
    public async Task Giang_vien_khong_thay_mon_hoc_cua_giang_vien_khac()
    {
        // Đây là ranh giới bảo mật quan trọng nhất của hệ thống nhiều giảng viên.
        var gv1 = await factory.LoginAsAsync("gv1@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv2@teachhub.local");

        var (subjectId, _) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv1);

        var ds = await gv2.GetFromJsonAsync<JsonElement>("/api/subjects");
        ds.EnumerateArray().Select(s => s.GetProperty("id").GetGuid())
          .ShouldNotContain(subjectId);

        var truyCap = await gv2.GetAsync($"/api/subjects/{subjectId}/versions");
        // 404 chứ không 403 — không xác nhận id có tồn tại.
        truyCap.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ma_mon_sai_dinh_dang_tra_ve_400()
    {
        var gv = await factory.LoginAsInstructorAsync();

        var res = await gv.PostAsJsonAsync("/api/subjects", new { code = "in-4203", name = "Sai mã" });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Sua_version_da_publish_tra_ve_409()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var res = await gv.PutAsJsonAsync($"/api/subject-versions/{versionId}",
            new { changelog = "sửa sau khi đã xuất bản" });

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_version_moi_tu_dong_archive_version_cu()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, v1) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        (await gv.PostAsync($"/api/subject-versions/{v1}/publish", null)).EnsureSuccessStatusCode();

        var tao = await gv.PostAsJsonAsync($"/api/subjects/{subjectId}/versions",
            new { cloned_from_version_id = v1, version_label = "2027-2028" });
        tao.EnsureSuccessStatusCode();
        var v2 = (await tao.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await gv.PostAsync($"/api/subject-versions/{v2}/publish", null)).EnsureSuccessStatusCode();

        var versions = await gv.GetFromJsonAsync<JsonElement>($"/api/subjects/{subjectId}/versions");
        var map = versions.EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetGuid(), x => x.GetProperty("status").GetString());

        map[v1].ShouldBe("archived");
        map[v2].ShouldBe("published");
    }

    [Fact]
    public async Task Publish_cap_nhat_current_version_id_cua_mon_hoc()
    {
        // current_version_id là thứ dùng làm mặc định khi mở lớp mới (SRS mục 3.2).
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, v1) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        (await gv.PostAsync($"/api/subject-versions/{v1}/publish", null)).EnsureSuccessStatusCode();

        var subject = await gv.GetFromJsonAsync<JsonElement>($"/api/subjects/{subjectId}");
        subject.GetProperty("current_version_id").GetGuid().ShouldBe(v1);
    }

    [Fact]
    public async Task Publish_hai_lan_tra_ve_409()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, v1) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);
        (await gv.PostAsync($"/api/subject-versions/{v1}/publish", null)).EnsureSuccessStatusCode();

        var lanHai = await gv.PostAsync($"/api/subject-versions/{v1}/publish", null);

        lanHai.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_version_cua_giang_vien_khac_tra_ve_404()
    {
        var gv1 = await factory.LoginAsAsync("gv-a@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv-b2@teachhub.local");
        var (_, v1) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv1);

        var res = await gv2.PostAsync($"/api/subject-versions/{v1}/publish", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
