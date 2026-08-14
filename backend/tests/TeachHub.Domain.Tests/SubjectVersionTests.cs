using Shouldly;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Tests;

public class SubjectVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    private static SubjectVersion Draft(string label = "2026-2027") =>
        SubjectVersion.CreateDraft(Guid.CreateVersion7(), label, Now);

    [Fact]
    public void Version_moi_tao_luon_o_trang_thai_draft()
    {
        Draft().Status.ShouldBe(SubjectVersionStatus.Draft);
    }

    [Fact]
    public void Chi_draft_moi_sua_duoc_noi_dung()
    {
        var v = Draft();
        v.CanEditContent.ShouldBeTrue();

        v.Publish(Now);
        v.CanEditContent.ShouldBeFalse();   // published là bất biến

        v.Archive(Now);
        v.CanEditContent.ShouldBeFalse();
    }

    [Fact]
    public void Publish_ghi_lai_thoi_diem_xuat_ban()
    {
        var v = Draft();
        v.Publish(Now);

        v.Status.ShouldBe(SubjectVersionStatus.Published);
        v.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Publish_hai_lan_la_loi()
    {
        var v = Draft();
        v.Publish(Now);

        Should.Throw<InvalidOperationException>(() => v.Publish(Now));
    }

    [Fact]
    public void Khong_the_publish_mot_version_da_archived()
    {
        // Muốn dùng lại nội dung cũ thì phải clone ra draft mới, không "hồi sinh"
        // version cũ — nếu không, các lớp đang tham chiếu nó sẽ đổi nghĩa dưới chân.
        var v = Draft();
        v.Publish(Now);
        v.Archive(Now);

        Should.Throw<InvalidOperationException>(() => v.Publish(Now));
    }

    [Fact]
    public void Archive_mot_draft_la_loi()
    {
        Should.Throw<InvalidOperationException>(() => Draft().Archive(Now));
    }

    [Fact]
    public void Nhan_version_khong_duoc_de_trong()
    {
        Should.Throw<ArgumentException>(() => Draft("   "));
    }

    [Fact]
    public void Clone_ghi_nho_nguon_va_van_la_draft()
    {
        var nguon = Guid.CreateVersion7();
        var v = SubjectVersion.CreateDraft(Guid.CreateVersion7(), "2027-2028", Now, clonedFrom: nguon);

        v.Status.ShouldBe(SubjectVersionStatus.Draft);
        v.ClonedFromVersionId.ShouldBe(nguon);
    }

    [Fact]
    public void Syllabus_deep_copy_khong_dung_chung_danh_sach_voi_ban_goc()
    {
        // Nếu DeepCopy chỉ gán tham chiếu, sửa chuẩn đầu ra ở bản 2027-2028 sẽ đổi luôn
        // đề cương của lớp năm 2026-2027 đã dạy xong — hỏng dữ liệu lịch sử, không phải bug hiển thị.
        var goc = new Syllabus
        {
            Credits = 3,
            Outcomes = [new LearningOutcome { Code = "CĐR1.1", Text = "Dựng được ứng dụng" }],
            References = ["React Native docs"],
        };

        var ban = goc.DeepCopy();
        ban.Outcomes.Add(new LearningOutcome { Code = "CĐR1.2", Text = "Thêm ở bản mới" });
        ban.References.Add("Sách mới");
        ban.Outcomes[0].Text = "Đã sửa";

        goc.Outcomes.Count.ShouldBe(1);
        goc.References.Count.ShouldBe(1);
        goc.Outcomes[0].Text.ShouldBe("Dựng được ứng dụng");
    }
}
