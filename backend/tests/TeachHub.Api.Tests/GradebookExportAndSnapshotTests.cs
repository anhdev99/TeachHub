using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Shouldly;

namespace TeachHub.Api.Tests;

/// <summary>
/// Đi hết đường dây thật: routing → policy → handler → cổng <c>IGradebookExcelWriter</c> đăng ký
/// trong DI. Test thuần của bộ dựng .xlsx không bắt được lỗi quên đăng ký cổng, mà quên đăng ký
/// là mọi lần xuất bảng điểm trả 500 — chỉ lộ ra khi giảng viên bấm nút.
/// </summary>
[Collection(TeachHubCollection.Ten)]
public class GradebookExportAndSnapshotTests(TeachHubApiFactory factory)
{
    /// <summary>
    /// <c>template_id = null</c> ⇒ <c>GradeColumnFactory.MacDinh</c> sinh sẵn thuong_ky 40% +
    /// cuoi_mon 60% = đúng 100%. Không dựng bộ cột mẫu riêng: mẫu không phải thứ đang kiểm ở
    /// đây, và mỗi bước seed thừa là một chỗ test này có thể đỏ vì lý do chẳng liên quan.
    /// </summary>
    private static async Task<Guid> SeedLopCoSoDiemAsync(HttpClient gv)
    {
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        (await gv.PostAsJsonAsync($"/api/offerings/{offeringId}/gradebook/init",
            new { template_id = (Guid?)null })).EnsureSuccessStatusCode();

        return offeringId;
    }

    [Fact]
    public async Task Xuat_mac_dinh_la_xlsx_mo_duoc_bang_ClosedXML()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        var res = await gv.GetAsync($"/api/offerings/{offeringId}/gradebook/export");
        res.EnsureSuccessStatusCode();

        res.Content.Headers.ContentType!.MediaType
            .ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        using var wb = new XLWorkbook(new MemoryStream(await res.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheet(1);

        // Chưa soạn mẫu xuất nào thì dùng mẫu mặc định: MSSV, Họ và tên, từng cột điểm,
        // Điểm cộng, Điểm tổng.
        ws.Cell(1, 1).GetString().ShouldBe("MSSV");
        ws.Cell(1, 3).GetString().ShouldBe("Thường kỳ");
        ws.Cell(1, 4).GetString().ShouldBe("Cuối môn");
    }

    [Fact]
    public async Task Duong_CSV_van_con_qua_tham_so_format()
    {
        // Bỏ CSV là lấy đi thứ đang chạy được. Không phụ thuộc thư viện nào, nên khi bộ sinh
        // .xlsx gặp sự cố với một bảng điểm cụ thể thì giảng viên vẫn lấy được dữ liệu ra.
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        var res = await gv.GetAsync($"/api/offerings/{offeringId}/gradebook/export?format=csv");
        res.EnsureSuccessStatusCode();

        res.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");

        var bytes = await res.Content.ReadAsByteArrayAsync();

        // BOM UTF-8 (EF BB BF) — thiếu nó thì Excel trên Windows đọc tiếng Việt thành ký tự rác.
        bytes[0].ShouldBe((byte)0xEF);
        bytes[1].ShouldBe((byte)0xBB);
        bytes[2].ShouldBe((byte)0xBF);
    }

    [Fact]
    public async Task Chot_so_ghi_mot_ban_co_hash()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/gradebook/snapshot", null);
        res.EnsureSuccessStatusCode();

        var ban = await res.Content.ReadFromJsonAsync<JsonElement>();
        ban.GetProperty("payload_hash").GetString().ShouldNotBeNullOrWhiteSpace();
        ban.GetProperty("payload_hash").GetString()!.Length.ShouldBe(64);

        var chiTiet = await gv.GetFromJsonAsync<JsonElement>(
            $"/api/gradebook/snapshots/{ban.GetProperty("id").GetGuid()}");

        chiTiet.GetProperty("payload_json").GetString()!.ShouldContain("thuong_ky");
    }

    [Fact]
    public async Task Chot_so_lan_hai_GHI_THEM_chu_khong_ghi_de()
    {
        // Bản chốt là bản chốt. Mở lại lớp rồi đóng lần nữa phải ra hai bản, không phải một
        // bản bị viết đè — nếu ghi đè thì không còn gì để đối chiếu, tức là mất luôn lý do
        // tồn tại của cơ chế này (ADR-006).
        var gv = await factory.LoginAsInstructorAsync();
        var offeringId = await SeedLopCoSoDiemAsync(gv);

        (await gv.PostAsync($"/api/offerings/{offeringId}/gradebook/snapshot", null))
            .EnsureSuccessStatusCode();
        (await gv.PostAsync($"/api/offerings/{offeringId}/gradebook/snapshot", null))
            .EnsureSuccessStatusCode();

        var ds = await gv.GetFromJsonAsync<JsonElement>(
            $"/api/offerings/{offeringId}/gradebook/snapshots");

        ds.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Trong_so_chua_du_100_phan_tram_thi_KHONG_chot_duoc()
    {
        // Đóng băng một bảng điểm sai thành "bản chính thức" còn tệ hơn không có bản nào:
        // về sau nó chính là thứ được đem ra đối chiếu khi sinh viên khiếu nại.
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);

        var res = await gv.PostAsync($"/api/offerings/{offeringId}/gradebook/snapshot", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lop_khong_ton_tai_tra_404_chu_khong_500()
    {
        var gv = await factory.LoginAsInstructorAsync();

        (await gv.PostAsync($"/api/offerings/{Guid.CreateVersion7()}/gradebook/snapshot", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await gv.GetAsync($"/api/gradebook/snapshots/{Guid.CreateVersion7()}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
