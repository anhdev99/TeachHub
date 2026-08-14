namespace TeachHub.Domain.Common;

/// <summary>
/// Base của mọi entity. Id sinh ở tầng ứng dụng (không để DB sinh) bằng UUIDv7
/// để vừa có tính duy nhất toàn cục vừa tăng dần theo thời gian — xem ADR trong
/// docs/architecture/00-tech-stack-decisions.html §1.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>Nội dung dạy học dùng xóa mềm để không phá dữ liệu bài nộp cũ.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}
