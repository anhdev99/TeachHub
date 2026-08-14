using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

/// <summary>
/// Bản tối thiểu cho Phase 1: chỉ đủ để versioning chạy đúng. Cấu hình slot nộp bài,
/// rubric và hạn nộp thuộc Phase 3.
///
/// <c>SubjectVersionId</c> là bổ sung so với SRS (Gap #3) và là thứ BẮT BUỘC cho versioning:
/// không có nó thì Assignment thuộc về Subject chứ không thuộc về phiên bản, và clone
/// phiên bản sẽ dùng chung bài tập với năm cũ.
/// </summary>
public class Assignment : Entity, ISoftDeletable
{
    public required Guid SubjectVersionId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Bản sao thuộc về phiên bản đích, Id mới hoàn toàn.</summary>
    public Assignment CloneTo(Guid targetVersionId) => new()
    {
        SubjectVersionId = targetVersionId,
        Title = Title,
        Description = Description,
    };
}
