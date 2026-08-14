using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Shouldly;

namespace TeachHub.Api.Tests;

public class EnrollmentImportTests(TeachHubApiFactory factory) : IClassFixture<TeachHubApiFactory>
{
    /// <summary>Dựng file .xlsx thật — không giả lập, vì bug hay nằm đúng ở chỗ ClosedXML đọc file.</summary>
    private static byte[] TaoFile(IEnumerable<(string Ma, string Ten, string Email)> rows,
                                  string headerMa = "MSSV", string headerTen = "Họ tên")
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Danh sách");
        ws.Cell(1, 1).Value = headerMa;
        ws.Cell(1, 2).Value = headerTen;
        ws.Cell(1, 3).Value = "Email";

        var i = 2;
        foreach (var (ma, ten, email) in rows)
        {
            ws.Cell(i, 1).Value = ma;
            ws.Cell(i, 2).Value = ten;
            ws.Cell(i, 3).Value = email;
            i++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task<HttpResponseMessage> ImportAsync(HttpClient gv, Guid offeringId, byte[] file)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(content, "file", "danh-sach.xlsx");

        return await gv.PostAsync($"/api/offerings/{offeringId}/enrollments/import", form);
    }

    private async Task<(HttpClient Gv, Guid OfferingId)> SeedAsync()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, _) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId);
        return (gv, offeringId);
    }

    private static List<(string, string, string)> DanhSach(int n) =>
        [.. Enumerable.Range(1, n).Select(i => ($"225101{i:D4}", $"Sinh viên {i}", $"sv{i}@sv.edu.vn"))];

    [Fact]
    public async Task Nghiem_thu_40_dong_co_2_trung_va_1_thieu_ho_ten_thi_nhap_37_va_bao_3_dong_loi()
    {
        var (gv, offeringId) = await SeedAsync();

        var rows = DanhSach(40);
        rows[10] = ("2251010001", "Trùng thứ nhất", "x1@sv.edu.vn");
        rows[20] = ("2251010002", "Trùng thứ hai", "x2@sv.edu.vn");
        rows[30] = ("2251010099", "", "x3@sv.edu.vn");

        var res = await ImportAsync(gv, offeringId, TaoFile(rows));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("imported").GetInt32().ShouldBe(37);
        var loi = body.GetProperty("errors").EnumerateArray().ToList();
        loi.Count.ShouldBe(3);
        loi.Select(e => e.GetProperty("row_number").GetInt32()).ShouldBe([12, 22, 32]);

        var ds = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        ds.GetArrayLength().ShouldBe(37);
    }

    [Fact]
    public async Task Sua_dong_loi_roi_import_lai_thi_du_40_va_khong_ai_bi_trung()
    {
        var (gv, offeringId) = await SeedAsync();

        var rows = DanhSach(40);
        rows[10] = ("2251010001", "Trùng thứ nhất", "x1@sv.edu.vn");
        await ImportAsync(gv, offeringId, TaoFile(rows));

        // Sửa dòng lỗi rồi nhập lại toàn bộ file
        var res = await ImportAsync(gv, offeringId, TaoFile(DanhSach(40)));
        res.EnsureSuccessStatusCode();

        var ds = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        ds.GetArrayLength().ShouldBe(40);

        var ma = ds.EnumerateArray().Select(e => e.GetProperty("student_code").GetString()!).ToList();
        ma.Distinct().Count().ShouldBe(40);
    }

    [Fact]
    public async Task Import_lai_cung_file_khong_tao_ban_ghi_trung()
    {
        var (gv, offeringId) = await SeedAsync();

        await ImportAsync(gv, offeringId, TaoFile(DanhSach(40)));
        var lanHai = await ImportAsync(gv, offeringId, TaoFile(DanhSach(40)));
        lanHai.EnsureSuccessStatusCode();

        var body = await lanHai.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("imported").GetInt32().ShouldBe(0);
        body.GetProperty("updated").GetInt32().ShouldBe(40);

        var ds = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        ds.GetArrayLength().ShouldBe(40);
    }

    [Fact]
    public async Task File_khong_co_cot_MSSV_bao_400_kem_danh_sach_header_doc_duoc()
    {
        var (gv, offeringId) = await SeedAsync();

        var file = TaoFile(DanhSach(3), headerMa: "Số thứ tự", headerTen: "Tên gọi");
        var res = await ImportAsync(gv, offeringId, file);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var text = await res.Content.ReadAsStringAsync();
        text.ShouldContain("Số thứ tự");   // nói rõ đọc được cột nào, để biết file sai chỗ nào
    }

    [Fact]
    public async Task File_khong_phai_excel_bao_400_chu_khong_phai_500()
    {
        var (gv, offeringId) = await SeedAsync();

        var res = await ImportAsync(gv, offeringId, "đây không phải file excel"u8.ToArray());

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nhan_dien_cot_theo_ten_header_du_thu_tu_cot_khac_di()
    {
        var (gv, offeringId) = await SeedAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("DS");
        ws.Cell(1, 1).Value = "Email";
        ws.Cell(1, 2).Value = "Họ và tên";
        ws.Cell(1, 3).Value = "Mã SV";
        ws.Cell(2, 1).Value = "a@sv.edu.vn";
        ws.Cell(2, 2).Value = "Nguyễn Văn A";
        ws.Cell(2, 3).Value = "2251012001";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var res = await ImportAsync(gv, offeringId, ms.ToArray());
        res.EnsureSuccessStatusCode();

        var ds = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        ds[0].GetProperty("student_code").GetString().ShouldBe("2251012001");
        ds[0].GetProperty("full_name").GetString().ShouldBe("Nguyễn Văn A");
    }

    [Fact]
    public async Task Import_vao_lop_cua_giang_vien_khac_tra_ve_404()
    {
        var (_, offeringId) = await SeedAsync();
        var gvKhac = await factory.LoginAsAsync("gv-import-khac@teachhub.local");

        var res = await ImportAsync(gvKhac, offeringId, TaoFile(DanhSach(3)));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenApi_khai_dung_multipart_co_field_nhi_phan()
    {
        var doc = await factory.CreateClient().GetStringAsync("/openapi/v1.json");
        using var json = JsonDocument.Parse(doc);

        var content = json.RootElement.GetProperty("paths")
            .GetProperty("/api/offerings/{id}/enrollments/import")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content");

        content.TryGetProperty("multipart/form-data", out var mp).ShouldBeTrue();

        var file = mp.GetProperty("schema").GetProperty("properties").GetProperty("file");
        var schema = file.TryGetProperty("$ref", out var r)
            ? json.RootElement.GetProperty("components").GetProperty("schemas")
                  .GetProperty(r.GetString()!.Split('/')[^1])
            : file;

        schema.GetProperty("format").GetString().ShouldBe("binary");
    }
}
