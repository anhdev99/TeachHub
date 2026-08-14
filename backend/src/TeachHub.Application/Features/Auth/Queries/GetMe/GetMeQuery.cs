using Microsoft.AspNetCore.Identity;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;

namespace TeachHub.Application.Features.Auth.Queries.GetMe;

public sealed record MeDto(Guid Id, string Email, string FullName, string[] Roles);

public sealed record GetMeQuery(Guid UserId) : IQuery<MeDto?>;

public sealed class GetMeHandler(UserManager<AppUser> users) : IQueryHandler<GetMeQuery, MeDto?>
{
    public async Task<MeDto?> Handle(GetMeQuery request, CancellationToken ct)
    {
        // Đọc lại DB thay vì bóc từ claim: claim là ảnh chụp lúc phát hành token, sống tới
        // 30 phút. Vừa đổi vai trò cho ai đó mà /api/auth/me vẫn trả vai cũ nửa tiếng thì
        // frontend hiện sai menu và người dùng báo "hệ thống không cho tôi vào" trong khi
        // quyền đã có. Một truy vấn theo khoá chính là cái giá rẻ để tránh chuyện đó.
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) return null;

        var roles = await users.GetRolesAsync(user);
        return new MeDto(user.Id, user.Email!, user.FullName, [.. roles]);
    }
}
