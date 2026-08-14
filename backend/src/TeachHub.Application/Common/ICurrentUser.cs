namespace TeachHub.Application.Common;

/// <summary>Danh tính người gọi, để behavior ghi log biết ai gây ra request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
