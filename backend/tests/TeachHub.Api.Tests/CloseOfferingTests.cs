using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

/// <summary>
/// Đóng lớp. Trước đây <c>OfferingStatus</c> có đủ ba giá trị nhưng KHÔNG endpoint nào đổi được
/// nó, nên <c>TakeGradeSnapshotCommand</c> không có ai gọi và một lớp dạy xong không để lại bản
/// đối chiếu nào — thứ chỉ lộ ra vào lúc sinh viên khiếu nại điểm, tức là quá muộn.
/// </summary>
[Collection(TeachHubCollection.Ten)]
public class CloseOfferingTests(TeachHubApiFactory factory)
{
    /// <summary>Lớp có sổ điểm mặc định: thuong_ky 40% + cuoi_mon 60% = đúng 100%.</summary>
    private static async Task<Guid> SeedLopCoSoDiemAsync(HttpClient gv)
    {
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        (await gv.PostAsJsonAsync($"/api/offerings/{offeringId}/gradebook/init",
            new { template_id = (Guid?)null })).EnsureSuccessStatusCode();

        return offeringId;
    }

    [Fact]
    public async Task Dong_lop_doi_trang_thai_VA_chot_so_diem_trong_cung_mot_lenh()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/close", null);
        res.EnsureSuccessStatusCode();

        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        kq.GetProperty("status").GetString().ShouldBe("closed");
        kq.GetProperty("snapshot").GetProperty("payload_hash").GetString()!.Length.ShouldBe(64);

        // Trạng thái phải THẬT SỰ đổi trong DB, không chỉ đúng trong phản hồi.
        var lop = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}");
        lop.GetProperty("status").GetString().ShouldBe("closed");

        // …và bản chốt phải nằm trong danh sách, đúng một bản.
        var ds = await gv.GetFromJsonAsync<JsonElement>(
            $"/api/offerings/{offeringId}/gradebook/snapshots");
        ds.GetArrayLength().ShouldBe(1);
        ds[0].GetProperty("id").GetGuid().ShouldBe(kq.GetProperty("snapshot").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Dong_mot_lop_da_dong_tra_409_va_KHONG_chot_them_ban_nao()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        (await gv.PostAsync($"/api/offerings/{offeringId}/close", null)).EnsureSuccessStatusCode();

        var lai = await gv.PostAsync($"/api/offerings/{offeringId}/close", null);
        lai.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Bấm nhầm nút hai lần không được sinh thêm một bản chốt trùng nội dung: danh sách bản
        // chốt là thứ giảng viên đọc để trả lời khiếu nại, và hai bản giống hệt nhau chỉ làm
        // câu hỏi "có gì đổi giữa hai lần đóng lớp" không trả lời được nữa.
        var ds = await gv.GetFromJsonAsync<JsonElement>(
            $"/api/offerings/{offeringId}/gradebook/snapshots");
        ds.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Lop_cua_giang_vien_khac_tra_404_chu_khong_bao_gio_403()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        var nguoiKhac = await factory.LoginAsAsync("gv-dong-lop@teachhub.local");

        var res = await nguoiKhac.PostAsync($"/api/offerings/{offeringId}/close", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Lớp chưa từng khởi tạo sổ điểm vẫn phải đóng được. Chặn lại là dựng một ngõ cụt cho
    /// giảng viên chỉ dùng phần nội dung — họ không có cách nào kết thúc lớp của mình.
    /// </summary>
    [Fact]
    public async Task Lop_chua_co_cot_diem_nao_van_dong_duoc_va_noi_ro_vi_sao_khong_co_ban_chot()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/close", null);
        res.EnsureSuccessStatusCode();

        var kq = await res.Content.ReadFromJsonAsync<JsonElement>();
        kq.GetProperty("status").GetString().ShouldBe("closed");
        kq.GetProperty("snapshot").ValueKind.ShouldBe(JsonValueKind.Null);

        // Không có bản chốt thì phải NÓI RA vì sao. Trả về đúng một chữ "closed" là để giảng
        // viên tự đoán xem sổ điểm đã được lưu lại hay chưa.
        kq.GetProperty("snapshot_skipped_reason").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Sổ điểm dựng dở (tổng trọng số ≠ 100%) thì KHÔNG đóng lớp. Đóng băng một bảng điểm sai
    /// thành "bản chính thức" còn tệ hơn không có bản nào — về sau nó là thứ được đem ra đối chiếu.
    /// </summary>
    [Fact]
    public async Task Trong_so_chua_hop_le_thi_tu_choi_dong_va_lop_VAN_dang_hoat_dong()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        // Phá trọng số thẳng dưới DB: đường ghi qua API đã chặn tổng ≠ 100%, nhưng dữ liệu cũ
        // và sửa tay vẫn tạo ra được trạng thái này.
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cot = await db.GradeColumns.FirstAsync(c => c.OfferingId == offeringId);
            cot.WeightPercent = 10m;
            await db.SaveChangesAsync();
        }

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/close", null);
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Điểm mấu chốt: lớp KHÔNG được đóng nửa vời. Đóng mà không có bản chốt là mất đúng
        // thứ mà việc đóng lớp sinh ra để có.
        var lop = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}");
        lop.GetProperty("status").GetString().ShouldBe("active");

        var ds = await gv.GetFromJsonAsync<JsonElement>(
            $"/api/offerings/{offeringId}/gradebook/snapshots");
        ds.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Sinh_vien_khong_dong_duoc_lop_minh_dang_hoc()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);
        var sv = await factory.LoginAsNewStudentOfAsync(offeringId);

        var res = await sv.PostAsync($"/api/offerings/{offeringId}/close", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
