using Shouldly;
using TeachHub.Domain.Assignments;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Giữ test dù phase chạy theo yêu cầu "không viết test": đây là phép cộng ra con số nộp
/// cho phòng đào tạo. Một lỗi làm tròn ở đây không có triệu chứng nào trên giao diện —
/// nó chỉ hiện ra khi sinh viên khiếu nại. Chạy < 10 ms.
/// </summary>
public class RubricTests
{
    private static Rubric Mau() => new()
    {
        Name = "Rubric bài tập lập trình",
        InstructorId = Guid.CreateVersion7(),
        Criteria =
        [
            new RubricCriterion { Description = "Chạy đúng yêu cầu", MaxPoints = 5m, OrderIndex = 0 },
            new RubricCriterion { Description = "Xử lý lỗi hợp lý", MaxPoints = 3m, OrderIndex = 1 },
            new RubricCriterion { Description = "Code sạch, có chú thích", MaxPoints = 2m, OrderIndex = 2 },
        ],
    };

    [Fact]
    public void Tong_diem_toi_da_bang_tong_cac_tieu_chi() => Mau().MaxPoints.ShouldBe(10m);

    [Fact]
    public void Cham_diem_cong_don_theo_tung_tieu_chi()
    {
        var r = Mau();

        r.Score(new Dictionary<Guid, decimal>
        {
            [r.Criteria[0].Id] = 4.5m,
            [r.Criteria[1].Id] = 2m,
            [r.Criteria[2].Id] = 1.5m,
        }).ShouldBe(8m);
    }

    [Fact]
    public void Cham_vuot_qua_max_cua_tieu_chi_la_loi()
    {
        var r = Mau();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 99m }));
    }

    [Fact]
    public void Cham_diem_am_la_loi()
    {
        var r = Mau();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = -1m }));
    }

    [Fact]
    public void Tieu_chi_khong_cham_duoc_tinh_la_0_chu_khong_bao_loi()
    {
        // Giảng viên chấm dở dang rồi lưu nháp — không được ném lỗi.
        var r = Mau();

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 5m }).ShouldBe(5m);
    }

    [Fact]
    public void Diem_luon_lam_tron_2_chu_so_thap_phan()
    {
        // decimal chứ không phải double — điểm số không bao giờ đi qua số thực dấu phẩy động.
        var r = new Rubric
        {
            Name = "x", InstructorId = Guid.CreateVersion7(),
            Criteria = [new RubricCriterion { Description = "a", MaxPoints = 10m, OrderIndex = 0 }],
        };

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 3.333m }).ShouldBe(3.33m);
    }

    [Fact]
    public void Lam_tron_o_ranh_gioi_di_ra_xa_khong_ve_so_chan()
    {
        // AwayFromZero, KHÔNG phải ToEven mặc định của .NET: 8.125 phải ra 8.13 chứ không
        // phải 8.12. Sinh viên bị làm tròn xuống ở ranh giới là khiếu nại đúng.
        var r = new Rubric
        {
            Name = "x", InstructorId = Guid.CreateVersion7(),
            Criteria = [new RubricCriterion { Description = "a", MaxPoints = 10m, OrderIndex = 0 }],
        };

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 8.125m }).ShouldBe(8.13m);
    }
}
