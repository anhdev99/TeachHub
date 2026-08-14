using Shouldly;
using TeachHub.Domain.Teaching;

namespace TeachHub.Domain.Tests;

public class JoinCodeTests
{
    [Fact]
    public void Ma_gom_6_ky_tu_viet_hoa_hoac_so()
    {
        JoinCode.Generate().Length.ShouldBe(6);
        JoinCode.Generate().ShouldAllBe(c => char.IsUpper(c) || char.IsDigit(c));
    }

    [Fact]
    public void Ma_khong_chua_ky_tu_de_nham_khi_doc_to_tren_lop()
    {
        // Giảng viên sẽ đọc mã này to cho cả lớp gõ theo. 0/O và 1/I/L gây gõ sai
        // nhiều tới mức đủ làm hỏng cả buổi học đầu tiên.
        var mau = string.Concat(Enumerable.Range(0, 500).Select(_ => JoinCode.Generate()));

        mau.ShouldNotContain("0");
        mau.ShouldNotContain("O");
        mau.ShouldNotContain("1");
        mau.ShouldNotContain("I");
        mau.ShouldNotContain("L");
    }

    [Fact]
    public void Ma_sinh_ra_du_phan_tan()
    {
        var tap = Enumerable.Range(0, 1000).Select(_ => JoinCode.Generate()).ToHashSet();
        tap.Count.ShouldBeGreaterThan(995);   // cho phép rất ít va chạm ngẫu nhiên
    }
}
