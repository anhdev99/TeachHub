using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace TeachHub.Api.Tests.Security;

public sealed class CrossInstructorIsolationTests(IsolationFixture fx) : IClassFixture<IsolationFixture>
{
    /// <summary>
    /// Mỗi dòng là một tài nguyên THUỘC VỀ giảng viên A; client bắn vào là của giảng viên B.
    /// Phase nào thêm loại tài nguyên mới thì thêm dòng vào đây — đó là điều kiện của
    /// Definition of Done mục 7, không phải việc tuỳ tâm.
    /// </summary>
    internal static readonly (string Method, string Template)[] TaiNguyen =
    [
        ("DELETE", "/api/instructor/invites/{inviteId}"),
        // Phase 1 bổ sung: subjects, subject-versions, chapters, lessons, lesson-components
        // Phase 2 bổ sung: offerings, enrollments, my-progress
        // Phase 3 bổ sung: assignments, submission-slots, submissions, file-tree, entry
        // Phase 4a bổ sung: groups, grade, final-assessments, dashboard
        // Phase 4b bổ sung: grade-columns, gradebook, bonus-points, export-excel, các *-templates
    ];

    /// <summary>Endpoint trả danh sách — không được lọt một phần tử nào của giảng viên khác.</summary>
    internal static readonly string[] DanhSach =
    [
        "/api/instructor/invites",
    ];

    public static TheoryData<string, string> TaiNguyenCuaGiangVienA()
    {
        var data = new TheoryData<string, string>();
        foreach (var (method, template) in TaiNguyen) data.Add(method, template);
        return data;
    }

    public static TheoryData<string> EndpointDanhSach()
    {
        var data = new TheoryData<string>();
        foreach (var url in DanhSach) data.Add(url);
        return data;
    }

    [Theory]
    [MemberData(nameof(TaiNguyenCuaGiangVienA))]
    public async Task Giang_vien_khac_luon_nhan_404(string method, string template)
    {
        var url = fx.Resolve(template);

        var res = await fx.GvB.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            $"{method} {url}: phải trả 404 cho giảng viên khác. Trả 403 là xác nhận id này CÓ TỒN TẠI, " +
            "đủ để dò ra cấu trúc dữ liệu của người khác chỉ bằng cách thử id.");
    }

    [Theory]
    [MemberData(nameof(EndpointDanhSach))]
    public async Task Danh_sach_khong_lot_du_lieu_giang_vien_khac(string url)
    {
        var json = await fx.GvB.GetFromJsonAsync<JsonElement>(url);

        json.GetArrayLength().ShouldBe(0,
            $"{url}: giảng viên B chưa tạo gì cả nên danh sách phải rỗng. Có phần tử nghĩa là " +
            "câu truy vấn thiếu điều kiện lọc instructor_id.");
    }

    [Fact]
    public async Task Chinh_chu_van_truy_cap_duoc_tai_nguyen_cua_minh()
    {
        // Nếu thiếu test này thì một handler trả 404 cho TẤT CẢ mọi người cũng làm
        // ma trận trên xanh — tức là ma trận không chứng minh được gì về việc lọc đúng.
        var ds = await fx.GvA.GetFromJsonAsync<JsonElement>("/api/instructor/invites");
        ds.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Tai_khoan_chi_co_vai_tro_tro_giang_bi_tu_choi_o_moi_endpoint_nghiep_vu()
    {
        // Canh gác cho ADR-013: vai trò TeachingAssistant tồn tại nhưng KHÔNG được cấp quyền gì.
        // Test này đỏ ngay nếu ai đó về sau vô tình thêm lại một policy cho nó.
        foreach (var (method, template) in TaiNguyen)
        {
            var url = fx.Resolve(template);
            var res = await fx.TroGiang.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

            res.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }

        foreach (var url in DanhSach)
        {
            var res = await fx.TroGiang.GetAsync(url);
            res.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }
    }
}
