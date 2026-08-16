using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Enrollments.Commands.CreateStudentAccounts;

public sealed record CreatedAccountDto(string StudentCode, string LoginEmail, string? InitialPassword, string Note);

public sealed record CreateStudentAccountsResponse(int Created, int Skipped, List<CreatedAccountDto> Accounts);

/// <summary>
/// The instructor clicks "Create accounts for the whole class" (SRS section 8). The initial
/// password is shown EXACTLY ONCE, right here; the system never stores the plaintext and there
/// is no way to look it up again.
/// </summary>
public sealed record CreateStudentAccountsCommand : ICommand<Result<CreateStudentAccountsResponse>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CreateStudentAccountsCommandHandler(
    IAppDbContext db,
    UserManager<AppUser> users,
    RoleManager<AppRole> roles)
    : ICommandHandler<CreateStudentAccountsCommand, Result<CreateStudentAccountsResponse>>
{
    public async Task<Result<CreateStudentAccountsResponse>> Handle(
        CreateStudentAccountsCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<CreateStudentAccountsResponse>.NotFound("Không tìm thấy lớp học phần.");

        if (!await roles.RoleExistsAsync(Roles.Student))
            await roles.CreateAsync(new AppRole(Roles.Student));

        var enrollments = await db.Enrollments
            .Where(e => e.OfferingId == request.OfferingId)
            .ToListAsync(ct);

        var results = new List<CreatedAccountDto>();
        var created = 0;
        var skipped = 0;

        foreach (var e in enrollments)
        {
            if (e.UserId is not null) { skipped++; continue; }

            // The school email is the login identifier when there is one; otherwise the student
            // code becomes the username.
            var loginEmail = !string.IsNullOrWhiteSpace(e.Email)
                ? e.Email!
                : $"{e.StudentCode}@sv.local";

            var existing = await users.FindByEmailAsync(loginEmail);
            if (existing is not null)
            {
                // The account already exists (the student attends several classes) — just attach
                // it to the enrollment, do not create it again.
                e.UserId = existing.Id;
                e.Status = EnrollmentStatus.Active;
                skipped++;
                results.Add(new CreatedAccountDto(e.StudentCode, loginEmail, null, "Tài khoản đã có sẵn, đã gắn vào lớp."));
                continue;
            }

            var password = InitialPassword.Generate();
            var user = new AppUser
            {
                UserName = loginEmail,
                Email = loginEmail,
                FullName = e.FullName,
                StudentCode = e.StudentCode,
                EmailConfirmed = true,
                MustChangePassword = true,   // force a change on first login (SRS section 8)
            };

            var createResult = await users.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                skipped++;
                results.Add(new CreatedAccountDto(e.StudentCode, loginEmail, null,
                    string.Join(" ", createResult.Errors.Select(x => x.Description))));
                continue;
            }

            await users.AddToRoleAsync(user, Roles.Student);
            e.UserId = user.Id;
            e.Status = EnrollmentStatus.Active;
            created++;

            results.Add(new CreatedAccountDto(e.StudentCode, loginEmail, password, "Đã tạo mới."));
        }

        await db.SaveChangesAsync(ct);

        return Result<CreateStudentAccountsResponse>.Ok(
            new CreateStudentAccountsResponse(created, skipped, results));
    }

}
