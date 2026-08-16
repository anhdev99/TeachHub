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
    public void Accepts_sources_on_the_allowlist(string url) =>
        VideoEmbedPolicy.IsAllowed(url).ShouldBeTrue();

    [Theory]
    [InlineData("https://evil.example/embed")]
    [InlineData("https://youtube.com.evil.example/embed")]      // lookalike domain
    [InlineData("http://www.youtube.com/embed/abc")]            // plain http
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//youtube.com/embed/abc")]                     // missing scheme
    [InlineData("")]
    public void Rejects_everything_else(string url) =>
        VideoEmbedPolicy.IsAllowed(url).ShouldBeFalse();

    [Fact]
    public void Rejects_unparseable_urls()
    {
        VideoEmbedPolicy.IsAllowed("khong phai url").ShouldBeFalse();
        VideoEmbedPolicy.IsAllowed(null!).ShouldBeFalse();
    }

    [Fact]
    public void Accepts_https_only()
    {
        // Embedding over http opens the door for a man in the middle to swap the video content
        // for something else, right inside a learning page carrying the school's domain name.
        VideoEmbedPolicy.IsAllowed("http://player.vimeo.com/video/123").ShouldBeFalse();
        VideoEmbedPolicy.IsAllowed("https://player.vimeo.com/video/123").ShouldBeTrue();
    }
}
