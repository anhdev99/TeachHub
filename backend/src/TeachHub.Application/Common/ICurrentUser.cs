namespace TeachHub.Application.Common;

/// <summary>Identity of the caller, so the logging behavior knows who triggered a request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
