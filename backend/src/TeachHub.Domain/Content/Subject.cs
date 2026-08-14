using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

public class Subject : Entity, IAuditable, ISoftDeletable
{
    public required string Code { get; set; }        // VD: IN4203
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required Guid InstructorId { get; set; }

    /// <summary>Bản published mới nhất — dùng làm mặc định khi mở lớp mới.</summary>
    public Guid? CurrentVersionId { get; set; }

    public List<SubjectVersion> Versions { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
