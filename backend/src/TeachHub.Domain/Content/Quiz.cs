using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

/// <summary>
/// Bản tối thiểu cho Phase 1 — ngân hàng câu hỏi, chấm tự động và cấu hình thời gian
/// thuộc Phase 5. Xem ghi chú <c>SubjectVersionId</c> ở <see cref="Assignment"/>.
/// </summary>
public class Quiz : Entity, ISoftDeletable
{
    public required Guid SubjectVersionId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Quiz CloneTo(Guid targetVersionId) => new()
    {
        SubjectVersionId = targetVersionId,
        Title = Title,
        Description = Description,
    };
}
