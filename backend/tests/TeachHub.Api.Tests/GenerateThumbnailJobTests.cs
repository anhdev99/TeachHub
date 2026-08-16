using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TeachHub.Application.Common;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Submissions;
using TeachHub.Infrastructure.Jobs;
using TeachHub.Infrastructure.Persistence;

namespace TeachHub.Api.Tests;

/// <summary>
/// Đầu-cuối cho <see cref="GenerateThumbnailJob"/>: đọc <c>SubmissionFile</c> từ CSDL thật, lưu
/// ảnh xem trước qua <c>IFileStorage</c> thật, rồi ghi <c>thumbnail_path</c> ngược lại.
///
/// <para>
/// <see cref="ThumbnailRendererTests"/> đã canh phần xử lý ảnh và <c>ThumbnailPolicyTests</c>
/// canh phần luật, nhưng cả hai đều chạy trên mảng byte trong bộ nhớ. Phần chưa ai kiểm là đúng
/// phần dễ hỏng âm thầm nhất: job đọc nhầm hàng, lưu vào đâu, và có ghi được đường dẫn trở lại
/// database hay không. Sai ở đó thì giao diện chấm bài hiện ô ảnh trống mãi mãi mà log không có
/// một dòng lỗi nào.
/// </para>
///
/// <para>
/// Bản ghi được ghi THẲNG qua <c>DbContext</c> chứ không đi qua endpoint tải tệp lên. Cố ý:
/// luồng upload thật gọi <c>ISubmissionFileQueue.EnqueueThumbnail</c>, và trong môi trường test
/// (Hangfire tắt) <c>InlineSubmissionFileQueue</c> chạy chính job này trên một Task nền không ai
/// await. Job khi đó có thể đã chạy xong trước khi test kịp khẳng định gì — và ca "chạy lại
/// không dựng lại" sẽ không phân biệt được với ca "dựng lần đầu".
/// </para>
/// </summary>
[Collection(TeachHubCollection.Ten)]
public class GenerateThumbnailJobTests(TeachHubApiFactory factory)
{
    /// <summary>Ảnh PNG thật, sinh bằng chính ImageSharp thay vì kèm một tệp nhị phân vào repo.</summary>
    private static byte[] PngHopLe(int rong = 640, int cao = 480)
    {
        using var anh = new Image<Rgba32>(rong, cao);

        // Vẽ nhiễu để khối IDAT đủ lớn: một ảnh trong suốt hoàn toàn nén lại còn vài chục byte,
        // và ca "ảnh hỏng" bên dưới cần có dữ liệu điểm ảnh thật để làm hỏng.
        for (var y = 0; y < cao; y++)
            for (var x = 0; x < rong; x++)
                anh[x, y] = new Rgba32((byte)(x * 7 % 256), (byte)(y * 13 % 256), (byte)((x + y) % 256), 255);

        using var ms = new MemoryStream();
        anh.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// PNG có chữ ký và IHDR hợp lệ nhưng dữ liệu điểm ảnh bị phá — bộ giải mã nhận ra định
    /// dạng, đọc được kích thước, rồi mới chết lúc giải nén.
    ///
    /// Phá byte TẠI CHỖ chứ không cắt ngắn tệp: cắt ngắn thì bộ giải mã chạm đáy stream và có
    /// thể ném <c>EndOfStreamException</c> — một ngoại lệ mà job KHÔNG bắt, nên test sẽ đỏ vì
    /// một tình huống khác hẳn tình huống đang muốn kiểm.
    /// </summary>
    private static byte[] PngHong()
    {
        var png = PngHopLe();

        // Bố cục PNG: 8 byte chữ ký, rồi chunk IHDR chiếm 25 byte (4 độ dài + 4 tên + 13 dữ
        // liệu + 4 CRC) — hết ở offset 33. Từ đó trở đi là các chunk mang dữ liệu ảnh.
        for (var i = 45; i < png.Length - 12; i++) png[i] = (byte)~png[i];

        return png;
    }

    /// <summary>Tệp ZIP thật — dùng cho ca "không phải ảnh".</summary>
    private static byte[] ZipHopLe()
    {
        using var ms = new MemoryStream();

        using (var zip = new System.IO.Compression.ZipArchive(
                   ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        using (var w = new StreamWriter(zip.CreateEntry("README.md").Open()))
            w.Write("# bai nop\n");

        return ms.ToArray();
    }

    // ── Bốn ca chính ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anh_hop_le_thi_dung_duoc_anh_xem_truoc_va_ghi_duong_dan_vao_CSDL()
    {
        var (submissionId, fileId) = await SeedTepAsync("minh-chung.png", PngHopLe());

        await ChayJobAsync(fileId);

        var tep = await DocTepAsync(fileId);

        tep.ThumbnailPath.ShouldNotBeNull(
            "Job chạy xong mà thumbnail_path vẫn null: hoặc job không tìm thấy hàng, hoặc " +
            "SaveChanges không được gọi. Giao diện chấm bài sẽ hiện ô ảnh trống mãi mãi.");

        // Thư mục riêng theo bài nộp: xóa bài nộp thì xóa được cả ảnh xem trước mà không đụng
        // tệp gốc, và ảnh xem trước không bao giờ lẫn vào danh sách tệp sinh viên đã nộp.
        //
        // Kỳ vọng là "thumbnails{id}" LIỀN NHAU chứ không phải "thumbnails/{id}", dù job truyền
        // vào chuỗi có dấu '/'. Cả hai adapter đều chạy container qua cùng một hàm Sanitize giữ
        // lại chữ, số, '-' và '_' — dấu '/' bị loại. Đó là hành vi THẬT và nhất quán giữa Local
        // với S3, nên test bám theo nó; khẳng định theo chuỗi mà job gõ ra sẽ đỏ ở cả hai provider.
        tep.ThumbnailPath!.StartsWith($"thumbnails{submissionId}/", StringComparison.Ordinal)
            .ShouldBeTrue(
                "Ảnh xem trước phải nằm trong thư mục riêng theo bài nộp, không nằm cạnh tệp gốc. " +
                $"Đường dẫn thật: {tep.ThumbnailPath}");

        // Tên tệp do storage tự đặt (UUIDv7 + phần mở rộng): tên job truyền vào chỉ dùng để lấy
        // đuôi. Khẳng định đuôi .jpg vì đó là thứ quyết định trình duyệt hiển thị được hay không.
        tep.ThumbnailPath.ShouldEndWith(".jpg");

        tep.ThumbnailPath.ShouldNotBe(tep.FilePath);

        var anhXemTruoc = await DocTuStorageAsync(tep.ThumbnailPath!);

        // Chữ ký JPEG: FF D8 FF. Khẳng định đúng ĐỊNH DẠNG ĐẦU RA chứ không chỉ "có byte" —
        // chép nguyên tệp PNG gốc sang cũng làm phép kiểm "không rỗng" xanh.
        anhXemTruoc.Length.ShouldBeGreaterThan(3);
        anhXemTruoc[0].ShouldBe((byte)0xFF);
        anhXemTruoc[1].ShouldBe((byte)0xD8);
        anhXemTruoc[2].ShouldBe((byte)0xFF);

        // Và đã THU NHỎ thật. Không kiểm chỗ này thì một job lưu lại nguyên ảnh 12 megapixel
        // vẫn xanh, trong khi màn hình chấm bài tải về đúng thứ nó muốn tránh.
        using var doc = Image.Load(anhXemTruoc);
        Math.Max(doc.Width, doc.Height).ShouldBe(TeachHub.Domain.Files.ThumbnailPolicy.ThumbnailMaxEdge);
        doc.Width.ShouldBe(320);
        doc.Height.ShouldBe(240);          // 640×480 giữ nguyên tỷ lệ, không cắt xén
    }

    [Fact]
    public async Task Da_co_anh_xem_truoc_thi_chay_lai_KHONG_dung_lai()
    {
        // Hangfire retry tới hai lần, và job cũng có thể được đẩy lại bằng tay. Dựng lại mỗi lần
        // là mỗi lần một tệp mới trong storage mà không ai xóa tệp cũ đi.
        const string daCo = "thumbnails/co-san/khong-ton-tai.jpg";
        var (_, fileId) = await SeedTepAsync("minh-chung.png", PngHopLe(), thumbnailPath: daCo);

        await ChayJobAsync(fileId);

        (await DocTepAsync(fileId)).ThumbnailPath.ShouldBe(daCo,
            "Job đã ghi đè một thumbnail_path có sẵn — mỗi lần retry sẽ để lại một tệp mồ côi.");

        // Và thật sự KHÔNG chạm vào storage: đường dẫn trên trỏ vào hư không ngay từ đầu, nên
        // nếu bây giờ đọc được thì job đã dựng lại rồi ghi đè lên chính chỗ đó.
        await Should.ThrowAsync<FileNotFoundException>(() => DocTuStorageAsync(daCo));
    }

    [Fact]
    public async Task Tep_khong_phai_anh_thi_bo_qua()
    {
        var (_, fileId) = await SeedTepAsync("bai-nop.zip", ZipHopLe());

        await ChayJobAsync(fileId);

        (await DocTepAsync(fileId)).ThumbnailPath.ShouldBeNull(
            "Tệp không phải ảnh mà vẫn có thumbnail_path: ThumbnailPolicy.IsImage đang bị bỏ qua, " +
            "và mọi tệp ZIP sinh viên nộp sẽ được đưa vào bộ giải mã ảnh.");
    }

    [Fact]
    public async Task Anh_hong_thi_ghi_log_roi_di_tiep_chu_KHONG_nem()
    {
        var (_, fileId) = await SeedTepAsync("anh-hong.png", PngHong());

        // KHÔNG ném là điều kiện sống còn, không phải chi tiết thẩm mỹ: ném ra thì Hangfire
        // retry, và mỗi vòng retry lại nạp đúng cái tệp không bao giờ xử lý được đó.
        await Should.NotThrowAsync(() => ChayJobAsync(fileId));

        (await DocTepAsync(fileId)).ThumbnailPath.ShouldBeNull();
    }

    // ── Ba ca biên còn lại của phần đọc–ghi ────────────────────────────────────────────

    [Fact]
    public async Task Tep_doi_duoi_thanh_png_bi_tu_choi_chu_khong_nem()
    {
        // Nhánh KHÁC hẳn ca "ảnh hỏng": ở đây Image.Identify trả về null (không nhận ra định
        // dạng) chứ không ném, nên job đi vào nhánh `kq.Content is null` — nhánh từ chối có lý
        // do, ghi log rồi dừng. Hai nhánh xử lý lỗi khác nhau, cần hai ca khác nhau.
        var (_, fileId) = await SeedTepAsync("thuc-ra-la-van-ban.png", "day khong phai anh"u8.ToArray());

        await Should.NotThrowAsync(() => ChayJobAsync(fileId));

        (await DocTepAsync(fileId)).ThumbnailPath.ShouldBeNull();
    }

    [Fact]
    public async Task Ban_ghi_da_bi_xoa_trong_luc_cho_thi_job_thoat_em_tham()
    {
        // Sinh viên xóa tệp ngay sau khi tải lên là chuyện thường, và job đã nằm trong hàng đợi
        // từ trước. Ném ở đây là Hangfire retry hai lần cho một hàng không bao giờ quay lại.
        await Should.NotThrowAsync(() => ChayJobAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Nop_bang_link_thi_khong_co_gi_de_doc()
    {
        // FilePath null = sinh viên dán link thay vì tải tệp lên (slot video_demo cho phép).
        var (_, fileId) = await SeedTepAsync("video.png", noiDung: null);

        await Should.NotThrowAsync(() => ChayJobAsync(fileId));

        (await DocTepAsync(fileId)).ThumbnailPath.ShouldBeNull();
    }

    // ── Hạ tầng ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Một bài nộp thật kèm một tệp. Lớp học phần, bài tập và slot đều dựng qua API để bản ghi
    /// thỏa mọi khóa ngoại thật — bản ghi bịa ra sẽ bị Postgres chặn chứ không âm thầm lọt qua
    /// như với InMemory provider.
    /// </summary>
    private async Task<(Guid SubmissionId, Guid FileId)> SeedTepAsync(
        string tenTep, byte[]? noiDung, string? thumbnailPath = null)
    {
        var gv = await factory.LoginAsInstructorAsync();
        var (subjectId, versionId) = await TeachHubApiFactory.SeedPublishedSubjectAsync(gv);
        var offeringId = await TeachHubApiFactory.CreateOfferingAsync(gv, subjectId, versionId);

        _ = await factory.LoginAsNewStudentOfAsync(offeringId);

        var res = await gv.PostAsJsonAsync("/api/assignments", new
        {
            subject_version_id = versionId,
            title = "Bài tập cho test ảnh xem trước",
            description = (string?)null,
            due_at = (DateTimeOffset?)null,
            allow_late_submission = true,
            late_penalty_percent_per_day = 0m,
            max_score = 10m,
            use_suggested_slots = true,
        });

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Seed thất bại: POST /api/assignments trả {(int)res.StatusCode} — " +
                await res.Content.ReadAsStringAsync());

        var baiTap = await res.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = baiTap.GetProperty("id").GetGuid();
        var slot = baiTap.GetProperty("slots").EnumerateArray()
            .First(s => s.GetProperty("slot_key").GetString() == "images");

        var ghiDanh = await gv.GetFromJsonAsync<JsonElement>($"/api/offerings/{offeringId}/enrollments");
        var enrollmentId = ghiDanh.EnumerateArray().First().GetProperty("id").GetGuid();

        var now = TimeProvider.System.GetUtcNow();

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var baiNop = new Submission
        {
            OwnerType = SlotOwnerType.Assignment,
            AssignmentId = assignmentId,
            OfferingId = offeringId,
            EnrollmentId = enrollmentId,
            SubmittedByType = SubmittedByType.Individual,
            AttemptNo = 1,
            IsCurrent = true,
            Status = SubmissionStatus.Submitted,
            SubmittedAt = now,
        };

        // Nội dung phải nằm trong storage THẬT: job mở stream từ đó, và một hàng trỏ vào tệp
        // không tồn tại sẽ ném FileNotFoundException — một ngoại lệ job cố ý KHÔNG bắt, nên
        // ca test sẽ đỏ vì hạ tầng seed chứ không vì hành vi đang kiểm.
        string? filePath = null;

        if (noiDung is not null)
            filePath = (await storage.SaveAsync($"submissions/{baiNop.Id}", tenTep,
                new MemoryStream(noiDung), "application/octet-stream", CancellationToken.None)).Path;

        var tep = new SubmissionFile
        {
            SubmissionId = baiNop.Id,
            SlotId = slot.GetProperty("id").GetGuid(),
            SlotKey = slot.GetProperty("slot_key").GetString()!,
            FileName = tenTep,
            FilePath = filePath,
            ThumbnailPath = thumbnailPath,
            SizeBytes = noiDung?.Length ?? 0,
            ContentType = "application/octet-stream",
            UploadedAt = now,
            ProcessingStatus = FileProcessingStatus.Done,
        };

        db.Submissions.Add(baiNop);
        db.SubmissionFiles.Add(tep);
        await db.SaveChangesAsync();

        return (baiNop.Id, tep.Id);
    }

    /// <summary>
    /// Chạy job trên một scope DI RIÊNG, đúng như Hangfire và <c>InlineSubmissionFileQueue</c>
    /// vẫn làm. Dùng lại scope của phần seed thì DbContext còn theo dõi sẵn bản ghi, và ca
    /// "đã có ảnh xem trước" sẽ xanh nhờ bộ nhớ đệm chứ không nhờ job đọc đúng từ database.
    /// </summary>
    private async Task ChayJobAsync(Guid fileId)
    {
        using var scope = factory.CreateServiceScope();
        var job = scope.ServiceProvider.GetRequiredService<GenerateThumbnailJob>();
        await job.RunAsync(fileId, CancellationToken.None);
    }

    private async Task<SubmissionFile> DocTepAsync(Guid fileId)
    {
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SubmissionFiles.AsNoTracking().SingleAsync(f => f.Id == fileId);
    }

    private async Task<byte[]> DocTuStorageAsync(string path)
    {
        using var scope = factory.CreateServiceScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        await using var stream = await storage.OpenReadAsync(path, CancellationToken.None);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
