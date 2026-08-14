namespace TeachHub.Domain.Content;

/// <summary>
/// Danh sách nguồn video được phép nhúng vào trang học.
///
/// Phải nằm ở Domain và phải được kiểm ở TẦNG API, không chỉ ở giao diện. Bản đầu tiên chỉ có
/// allowlist trong <c>frontend/.../xem-noi-dung.tsx</c> — ai gọi thẳng API là đặt được
/// <c>file_url</c> tuỳ ý cho bài loại <c>video_embed</c> và nhúng trang lừa đảo vào trang học,
/// hiển thị dưới tên miền của trường. Chặn ở giao diện chỉ ngăn được người dùng bình thường.
/// </summary>
public static class VideoEmbedPolicy
{
    private static readonly string[] HostChoPhep =
    [
        "youtube.com",
        "www.youtube.com",
        "youtube-nocookie.com",
        "www.youtube-nocookie.com",
        "youtu.be",
        "player.vimeo.com",
        "vimeo.com",
    ];

    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // UriKind.Absolute: "//youtube.com/..." và "javascript:..." đều không qua được.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        // Chỉ https. Nhúng qua http là mở đường cho người đứng giữa thay nội dung video
        // bằng thứ khác, ngay trong trang mang tên miền của trường.
        if (uri.Scheme != Uri.UriSchemeHttps) return false;

        // So khớp CHÍNH XÁC hostname, không dùng EndsWith/Contains: "youtube.com.evil.example"
        // kết thúc bằng "evil.example" nhưng chứa "youtube.com" — Contains sẽ cho nó lọt.
        return HostChoPhep.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }
}
