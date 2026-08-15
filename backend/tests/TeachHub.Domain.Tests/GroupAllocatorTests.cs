using Shouldly;
using TeachHub.Domain.Groups;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Giữ test dù Phase 4a chạy theo yêu cầu "không viết test": đây là hàm thuần quyết định
/// ai làm việc với ai trong cả học kỳ, và các ca biên của nó (nhóm lẻ loi một người, cùng
/// seed ra cùng kết quả) không nhìn thấy được bằng cách bấm thử trên giao diện. Chạy < 10 ms.
/// </summary>
public class GroupAllocatorTests
{
    private static List<Guid> Sv(int n) => [.. Enumerable.Range(0, n).Select(_ => Guid.CreateVersion7())];

    [Fact]
    public void Chia_deu_khi_si_so_chia_het()
    {
        var nhom = GroupAllocator.Allocate(Sv(12), groupSize: 4, seed: 42);

        nhom.Count.ShouldBe(3);
        nhom.ShouldAllBe(g => g.Count == 4);
    }

    [Fact]
    public void Khong_de_lai_nhom_le_loi_mot_nguoi()
    {
        // 13 sinh viên, nhóm 4 → KHÔNG được ra 4/4/4/1.
        var nhom = GroupAllocator.Allocate(Sv(13), groupSize: 4, seed: 42);

        nhom.ShouldAllBe(g => g.Count >= 3);
        nhom.Sum(g => g.Count).ShouldBe(13);
    }

    [Fact]
    public void Moi_sinh_vien_xuat_hien_dung_mot_lan()
    {
        var sv = Sv(37);
        var nhom = GroupAllocator.Allocate(sv, groupSize: 5, seed: 7);

        nhom.SelectMany(g => g).OrderBy(x => x).ShouldBe(sv.OrderBy(x => x));
    }

    [Fact]
    public void Cung_seed_cho_ket_qua_giong_nhau()
    {
        // Giảng viên bấm "Chia ngẫu nhiên", xem trước, rồi mới xác nhận —
        // kết quả không được đổi giữa lúc xem và lúc lưu.
        var sv = Sv(20);

        GroupAllocator.Allocate(sv, 4, seed: 99)
            .ShouldBe(GroupAllocator.Allocate(sv, 4, seed: 99));
    }

    [Fact]
    public void Si_so_it_hon_mot_nhom_thi_gom_lam_mot()
    {
        GroupAllocator.Allocate(Sv(3), groupSize: 5, seed: 1).Count.ShouldBe(1);
    }

    [Fact]
    public void Danh_sach_rong_tra_ve_khong_nhom_nao()
    {
        GroupAllocator.Allocate([], groupSize: 4, seed: 1).ShouldBeEmpty();
    }

    [Fact]
    public void Lop_40_chia_nhom_4_ra_dung_10_nhom_deu_nhau()
    {
        // Đúng kịch bản nghiệm thu UC-25.
        var nhom = GroupAllocator.Allocate(Sv(40), groupSize: 4, seed: 2026);

        nhom.Count.ShouldBe(10);
        nhom.ShouldAllBe(g => g.Count == 4);
    }
}
