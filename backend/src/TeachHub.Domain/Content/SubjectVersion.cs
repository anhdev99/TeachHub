using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

public enum SubjectVersionStatus { Draft, Published, Archived }

public class SubjectVersion : Entity
{
    private SubjectVersion() { }   // cho EF Core

    public Guid SubjectId { get; private set; }
    public string VersionLabel { get; private set; } = null!;
    public SubjectVersionStatus Status { get; private set; }
    public Syllabus Syllabus { get; set; } = new();
    public Guid? ClonedFromVersionId { get; private set; }
    public string? Changelog { get; set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public List<Chapter> Chapters { get; private set; } = [];

    /// <summary>
    /// Chỉ bản draft mới sửa được. Đây là bảo đảm cốt lõi khiến các lớp học phần
    /// đã mở không bị đổi nội dung dưới chân (SRS mục 5.2).
    /// </summary>
    public bool CanEditContent => Status == SubjectVersionStatus.Draft;

    public static SubjectVersion CreateDraft(Guid subjectId, string label, DateTimeOffset now,
                                             Guid? clonedFrom = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Nhãn phiên bản không được để trống.", nameof(label));

        return new SubjectVersion
        {
            SubjectId = subjectId,
            VersionLabel = label.Trim(),
            Status = SubjectVersionStatus.Draft,
            ClonedFromVersionId = clonedFrom,
            CreatedAt = now,
        };
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != SubjectVersionStatus.Draft)
            throw new InvalidOperationException(
                $"Chỉ xuất bản được bản nháp. Phiên bản '{VersionLabel}' đang ở trạng thái {Status}.");

        Status = SubjectVersionStatus.Published;
        PublishedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status != SubjectVersionStatus.Published)
            throw new InvalidOperationException(
                $"Chỉ lưu trữ được phiên bản đã xuất bản. '{VersionLabel}' đang ở {Status}.");

        Status = SubjectVersionStatus.Archived;
    }
}
