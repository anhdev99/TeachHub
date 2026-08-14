using Shouldly;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Tests;

public class VideoEmbedPolicyTests
{
    [Theory]
    [InlineData("https://www.youtube.com/embed/abc123")]
    [InlineData("https://youtube.com/embed/abc123")]
    [InlineData("https://www.youtube-nocookie.com/embed/abc123")]
    [InlineData("https://youtu.be/abc123")]
    [InlineData("https://player.vimeo.com/video/123")]
    [InlineData("https://vimeo.com/123")]
    public void Chap_nhan_nguon_trong_danh_sach(string url) =>
        VideoEmbedPolicy.IsAllowed(url).ShouldBeTrue();

    [Theory]
    [InlineData("https://evil.example/embed")]
    [InlineData("https://youtube.com.evil.example/embed")]      // tên miền lừa
    [InlineData("http://www.youtube.com/embed/abc")]            // http trần
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//youtube.com/embed/abc")]                     // thiếu scheme
    [InlineData("")]
    public void Tu_choi_moi_thu_khac(string url) =>
        VideoEmbedPolicy.IsAllowed(url).ShouldBeFalse();

    [Fact]
    public void Tu_choi_URL_khong_phan_tich_duoc()
    {
        VideoEmbedPolicy.IsAllowed("khong phai url").ShouldBeFalse();
        VideoEmbedPolicy.IsAllowed(null!).ShouldBeFalse();
    }

    [Fact]
    public void Chi_chap_nhan_https()
    {
        // Nhúng qua http là mở đường cho người đứng giữa thay nội dung video bằng thứ khác,
        // ngay trong trang học mang tên miền của trường.
        VideoEmbedPolicy.IsAllowed("http://player.vimeo.com/video/123").ShouldBeFalse();
        VideoEmbedPolicy.IsAllowed("https://player.vimeo.com/video/123").ShouldBeTrue();
    }
}
