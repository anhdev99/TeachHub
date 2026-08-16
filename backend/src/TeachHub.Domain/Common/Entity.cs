namespace TeachHub.Domain.Common;

/// <summary>
/// Base class of every entity. The Id is generated in the application layer (never by the DB)
/// as a UUIDv7, so it is both globally unique and time-ordered — see the ADR in
/// docs/architecture/00-tech-stack-decisions.html §1.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>Teaching content uses soft delete so that old submission data is not destroyed.</summary>
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
