using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class OfferingsControllerTests(TeachHubApiFactory factory)
{
    [Fact]
    public async Task Mo_lop_mac_dinh_dung_phien_ban_dang_published()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, v1) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);

        var res = await gv.PostAsJsonAsync("/api/offerings",
            new { subject_id = subjectId, semester = "HK1 2026-2027", class_code = "DA24TIN01" });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var offering = await res.Content.ReadFromJsonAsync<JsonElement>();
        offering.GetProperty("subject_version_id").GetGuid().ShouldBe(v1);
    }

    [Fact]
    public async Task Mo_lop_chon_duoc_mot_phien_ban_cu_cu_the()
    {
        // UC-06b: dạy lại đúng nội dung năm ngoái cho một lớp bù.
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, v1) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        await TeachHubApiFactory.CloneAndPublishAsync(gv, subjectId, v1, "2027-2028");

        var res = await gv.PostAsJsonAsync("/api/offerings", new
        {
            subject_id = subjectId,
            subject_version_id = v1,
            semester = "HK1 2027-2028",
            class_code = "LOP-BU",
        });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("subject_version_id").GetGuid().ShouldBe(v1);
    }

    [Fact]
    public async Task Tao_phien_ban_moi_KHONG_lam_doi_noi_dung_cua_lop_dang_chay()
    {
        // Bất biến quan trọng nhất của cả hệ thống.
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, v1) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        await TeachHubApiFactory.CloneAndPublishAsync(gv, subjectId, v1, "2027-2028");

        var offering = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}");
        offering.GetProperty("subject_version_id").GetGuid().ShouldBe(v1);   // vẫn là v1
    }

    [Fact]
    public async Task Khong_mo_duoc_lop_tu_phien_ban_dang_draft()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, draftId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var res = await gv.PostAsJsonAsync("/api/offerings", new
        {
            subject_id = subjectId,
            subject_version_id = draftId,
            semester = "HK1",
            class_code = "X",
        });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mo_lop_sinh_ma_lop_doc_duoc_va_khong_trung()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);

        var a = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, classCode: "LOP-A");
        var b = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, classCode: "LOP-B");

        var ma = new List<string>();
        foreach (var id in new[] { a, b })
        {
            var o = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{id}");
            var code = o.GetProperty("join_code").GetString()!;
            code.Length.ShouldBe(6);
            code.ShouldNotContain("O");   // ký tự dễ đọc nhầm đã bị loại
            ma.Add(code);
        }

        ma[0].ShouldNotBe(ma[1]);
    }

    [Fact]
    public async Task Lop_cua_giang_vien_khac_tra_ve_404_khong_phai_403()
    {
        var gv1 = await factory.LoginAsAsync("gv-lop-1@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv-lop-2@teachhub.local");

        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv1);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv1, subjectId);

        (await gv2.GetAsync($"/api/offerings/{offeringId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mo_lop_tu_mon_cua_giang_vien_khac_tra_ve_404()
    {
        var gv1 = await factory.LoginAsAsync("gv-lop-3@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv-lop-4@teachhub.local");
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv1);

        var res = await gv2.PostAsJsonAsync("/api/offerings",
            new { subject_id = subjectId, semester = "HK1", class_code = "CUOP" });

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Danh_sach_lop_chi_tra_lop_cua_chinh_minh()
    {
        var gv1 = await factory.LoginAsAsync("gv-lop-5@teachhub.local");
        var gv2 = await factory.LoginAsAsync("gv-lop-6@teachhub.local");
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv1);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv1, subjectId);

        var cuaGv2 = await gv2.GetFromJsonAsync<JsonElement>("/api/offerings");

        cuaGv2.EnumerateArray().Select(o => o.GetProperty("id").GetGuid())
              .ShouldNotContain(offeringId);
    }
}
