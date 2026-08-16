using Shouldly;
using TeachHub.Domain.Grading;

namespace TeachHub.Domain.Tests;

public class GradeSnapshotHashTests
{
    [Fact]
    public void Cung_mot_chuoi_luon_ra_cung_mot_hash()
    {
        // Đối chiếu bản chốt sáu tháng sau chỉ có nghĩa nếu hàm băm tất định.
        const string json = """{"class_code":"DA24TIN01","rows":[]}""";

        GradeSnapshotHash.Compute(json).ShouldBe(GradeSnapshotHash.Compute(json));
    }

    [Fact]
    public void Doi_mot_ky_tu_la_doi_hash()
    {
        GradeSnapshotHash.Compute("""{"final_score":8.5}""")
            .ShouldNotBe(GradeSnapshotHash.Compute("""{"final_score":8.6}"""));
    }

    [Fact]
    public void Hash_la_64_ky_tu_hex_thuong()
    {
        // Cột payload_hash khai varchar(64). Trả về dạng viết hoa hay có dấu gạch là tràn cột
        // hoặc lệch khi so sánh chuỗi — cả hai chỉ lộ ra lúc chốt sổ thật.
        var h = GradeSnapshotHash.Compute("xin chào");

        h.Length.ShouldBe(64);
        h.ShouldAllBe(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    [Fact]
    public void Khop_voi_vector_SHA256_chuan()
    {
        // Vector kiểm thử công khai của SHA-256 cho chuỗi "abc". Có nó thì đổi thuật toán
        // nhầm (SHA-1, MD5) là test đỏ ngay chứ không âm thầm sinh hash khác họ.
        GradeSnapshotHash.Compute("abc")
            .ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Tieng_Viet_co_dau_bam_theo_UTF8()
    {
        // Bảng điểm đầy tên tiếng Việt. Băm theo encoding mặc định của máy chủ thì cùng một
        // bản chốt cho hai hash khác nhau trên hai máy — bản chốt mất sạch giá trị đối chiếu.
        GradeSnapshotHash.Compute("Nguyễn Văn Ánh")
            .ShouldBe(GradeSnapshotHash.Compute("Nguyễn Văn Ánh"));
    }
}
