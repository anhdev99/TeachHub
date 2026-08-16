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
        // Re-read from the DB instead of unpacking the claims: a claim is a snapshot taken when
        // the token was issued and it lives for up to 30 minutes. Change someone's role and have
        // /api/auth/me keep returning the old role for half an hour, and the frontend renders the
        // wrong menu while the user reports "the system won't let me in" even though the
        // permission is already there. One primary-key lookup is a cheap price to avoid that.
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) return null;

        var roles = await users.GetRolesAsync(user);
        return new MeDto(user.Id, user.Email!, user.FullName, [.. roles]);
    }
}
