using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests;

[Collection(TeachHubCollection.Ten)]
public class LessonUploadTests(TeachHubApiFactory factory)
{
    private static async Task<Guid> SeedLessonAsync(HttpClient gv)
    {
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters", new { title = "C1" });
        ch.EnsureSuccessStatusCode();
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var l = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "B1" });
        l.EnsureSuccessStatusCode();
        return (await l.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient gv, Guid lessonId, string ten, byte[] noiDung)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(noiDung);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", ten);

        return await gv.PostAsync($"/api/lessons/{lessonId}/file", form);
    }

    [Theory]
    [InlineData("bai-giang.exe")]
    [InlineData("bai-giang.pdf.exe")]   // đuôi kép
    [InlineData("bai-giang.pdf")]       // đuôi đúng nhưng nội dung là MZ (exe)
    public async Task Tu_choi_file_khong_dung_dinh_dang_bai_giang(string ten)
    {
        var gv = await factory.LoginAsInstructorAsync();
        var lessonId = await SeedLessonAsync(gv);

        var res = await UploadAsync(gv, lessonId, ten, [0x4D, 0x5A, 0x90, 0x00]);   // "MZ" = exe

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chap_nhan_pdf_that_va_ghi_lai_duong_dan_vao_bai_hoc()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var lessonId = await SeedLessonAsync(gv);
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x25];   // "%PDF-1.7"

        var res = await UploadAsync(gv, lessonId, "slide.pdf", pdf);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("file_url").GetString().ShouldStartWith("lessons/");

        // Bài học phải nhớ đường dẫn và tự suy đúng loại nội dung.
        var lesson = await gv.GetFromJsonAsync<JsonElement>($"/api/lessons/{lessonId}");
        lesson.GetProperty("file_url").GetString().ShouldNotBeNullOrWhiteSpace();
        lesson.GetProperty("content_type").GetString().ShouldBe("pdf");
    }

    [Fact]
    public async Task Noi_dung_file_luu_xuong_nguyen_ven_khong_mat_may_byte_dau()
    {
        // Handler đọc vài byte đầu để kiểm chữ ký; nếu quên ghép lại phần đã đọc thì file
        // lưu xuống mất mấy byte đầu và PDF hỏng — mở ra mới biết.
        var gv = await factory.LoginAsInstructorAsync();
        var lessonId = await SeedLessonAsync(gv);
        byte[] pdf = [.. "%PDF-1.7\nNội dung thật của slide"u8.ToArray()];

        var res = await UploadAsync(gv, lessonId, "slide.pdf", pdf);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("size_bytes").GetInt64().ShouldBe(pdf.Length);
    }

    [Fact]
    public async Task Khong_upload_duoc_vao_version_da_publish()
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (_, versionId) = await TeachHubApiFactory.SeedSubjectWithDraftAsync(gv);

        var ch = await gv.PostAsJsonAsync($"/api/subject-versions/{versionId}/chapters", new { title = "C1" });
        var chapterId = (await ch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var l = await gv.PostAsJsonAsync($"/api/chapters/{chapterId}/lessons", new { title = "B1" });
        var lessonId = (await l.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await gv.PostAsync($"/api/subject-versions/{versionId}/publish", null)).EnsureSuccessStatusCode();

        var res = await UploadAsync(gv, lessonId, "slide.pdf", [0x25, 0x50, 0x44, 0x46]);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OpenApi_khai_dung_multipart_co_field_nhi_phan()
    {
        // Đọc bằng JsonElement, KHÔNG so chuỗi: tài liệu in đẹp có khoảng trắng nên
        // Contains("\"format\":\"binary\"") sẽ trượt dù schema hoàn toàn đúng.
        var doc = await factory.CreateClient().GetStringAsync("/openapi/v1.json");
        using var json = JsonDocument.Parse(doc);

        var post = json.RootElement
            .GetProperty("paths")
            .GetProperty("/api/lessons/{id}/file")
            .GetProperty("post");

        var content = post.GetProperty("requestBody").GetProperty("content");
        content.TryGetProperty("multipart/form-data", out var multipart)
            .ShouldBeTrue("endpoint upload phải khai multipart/form-data, không phải x-www-form-urlencoded");

        var file = multipart.GetProperty("schema").GetProperty("properties").GetProperty("file");

        // .NET 10 khai field nhị phân bằng $ref tới components/schemas/IFormFile chứ không
        // nội tuyến, nên phải đi theo tham chiếu. Đây chính là lý do test đọc bằng JsonElement:
        // so chuỗi "format\":\"binary\"" sẽ trượt dù schema hoàn toàn đúng.
        var schema = file.TryGetProperty("$ref", out var refNode)
            ? json.RootElement.GetProperty("components").GetProperty("schemas")
                  .GetProperty(refNode.GetString()!.Split('/')[^1])
            : file;

        schema.GetProperty("type").GetString().ShouldBe("string");
        schema.GetProperty("format").GetString().ShouldBe("binary");

        // Phản hồi chỉ được khai application/json: text/plain lọt vào là client TypeScript
        // sinh ra một union thừa và người dùng phải tự đoán nhánh nào là thật.
        var kieuPhanHoi = post.GetProperty("responses").GetProperty("200")
            .GetProperty("content").EnumerateObject().Select(p => p.Name).ToArray();
        kieuPhanHoi.ShouldBe(["application/json"]);
    }
}
