using TeachHub.Domain.Common;

namespace TeachHub.Domain.Assignments;

public enum SlotOwnerType { Assignment, FinalAssessment }

/// <summary>
/// Một "ô nộp" do giảng viên tự khai: tên, định dạng nhận, số file, dung lượng, bắt buộc hay không.
/// Đây là điểm khác biệt so với thiết kế cũ khóa cứng vào ZIP (SRS mục 5.4): bài này cần
/// source + ảnh + video, bài kia chỉ cần source — xóa bớt slot là xong, không phải đổi code.
/// </summary>
public class SubmissionSlot : Entity
{
    public SlotOwnerType OwnerType { get; set; } = SlotOwnerType.Assignment;

    // ADR-003: hai FK thật thay cho owner_id đa hình, có CHECK constraint ở DB canh giữ.
    public Guid? AssignmentId { get; set; }
    public Guid? FinalAssessmentId { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>Khóa kỹ thuật do giảng viên đặt: source_code, images, report…</summary>
    public required string SlotKey { get; set; }

    public required string Label { get; set; }
    public List<string> AcceptedFileTypes { get; set; } = [];
    public bool AllowLinkInsteadOfFile { get; set; }
    public int MaxFiles { get; set; } = 1;
    public int MaxFileSizeMb { get; set; } = 50;
    public bool IsRequired { get; set; }

    /// <summary>Chỉ có nghĩa với slot nhận .zip — lưu jsonb.</summary>
    public RequiredStructure? RequiredStructure { get; set; }

    /// <summary>
    /// So khớp bằng <see cref="Path.GetExtension"/> nên "bai1.zip.exe" trả ".exe" và bị từ chối:
    /// đuôi kép là cách cũ nhất để lách bộ lọc định dạng.
    /// </summary>
    public bool Accepts(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return AcceptedFileTypes.Any(t => string.Equals(t, ext, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasRoomFor(int currentCount) => currentCount < MaxFiles;

    public bool AcceptsZip =>
        AcceptedFileTypes.Any(t => string.Equals(t, ".zip", StringComparison.OrdinalIgnoreCase));

    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;

    public SubmissionSlot CloneForNewOwner() => new()
    {
        OwnerType = OwnerType,
        OrderIndex = OrderIndex,
        SlotKey = SlotKey,
        Label = Label,
        AcceptedFileTypes = [.. AcceptedFileTypes],
        AllowLinkInsteadOfFile = AllowLinkInsteadOfFile,
        MaxFiles = MaxFiles,
        MaxFileSizeMb = MaxFileSizeMb,
        IsRequired = IsRequired,
        RequiredStructure = RequiredStructure?.DeepCopy(),
    };
}

/// <summary>
/// Cấu trúc thư mục bắt buộc bên trong file nén. Thiếu thì gắn cờ cho giảng viên,
/// KHÔNG chặn nộp (SRS UC-08 bước 4).
/// </summary>
public class RequiredStructure
{
    public List<string> RequiredFiles { get; set; } = [];
    public List<string> RequiredFolders { get; set; } = [];

    public RequiredStructure DeepCopy() => new()
    {
        RequiredFiles = [.. RequiredFiles],
        RequiredFolders = [.. RequiredFolders],
    };
}

/// <summary>
/// Mẫu gợi ý, KHÔNG phải ràng buộc. Giảng viên xóa/sửa/thêm thoải mái sau khi bấm —
/// đây chính là điểm khác biệt so với thiết kế cũ khóa cứng vào ZIP (SRS mục 5.4).
/// </summary>
public static class SlotTemplates
{
    public static IEnumerable<SubmissionSlot> Suggested() =>
    [
        new()
        {
            OrderIndex = 0, SlotKey = "source_code", Label = "Source code (nén ZIP)",
            AcceptedFileTypes = [".zip"], MaxFiles = 1, MaxFileSizeMb = 50, IsRequired = true,
            RequiredStructure = new RequiredStructure
            {
                RequiredFiles = ["README.md"], RequiredFolders = ["src"],
            },
        },
        new()
        {
            OrderIndex = 1, SlotKey = "images", Label = "Hình ảnh minh chứng",
            AcceptedFileTypes = [".jpg", ".jpeg", ".png"], MaxFiles = 10,
            MaxFileSizeMb = 10, IsRequired = false,
        },
        new()
        {
            OrderIndex = 2, SlotKey = "video_demo", Label = "Video minh chứng",
            AcceptedFileTypes = [".mp4"], MaxFiles = 1, MaxFileSizeMb = 200,
            IsRequired = false, AllowLinkInsteadOfFile = true,
        },
    ];
}
