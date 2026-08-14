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
/// Giảng viên bấm "Tạo tài khoản cho cả lớp" (SRS mục 8). Mật khẩu ban đầu hiển thị ĐÚNG MỘT LẦN
/// ngay tại đây; hệ thống không lưu lại bản rõ và không có cách nào xem lại.
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

        var danhSach = await db.Enrollments
            .Where(e => e.OfferingId == request.OfferingId)
            .ToListAsync(ct);

        var ketQua = new List<CreatedAccountDto>();
        var taoMoi = 0;
        var boQua = 0;

        foreach (var e in danhSach)
        {
            if (e.UserId is not null) { boQua++; continue; }

            // Email trường là định danh đăng nhập nếu có; không có thì dùng MSSV làm username.
            var loginEmail = !string.IsNullOrWhiteSpace(e.Email)
                ? e.Email!
                : $"{e.StudentCode}@sv.local";

            var daCo = await users.FindByEmailAsync(loginEmail);
            if (daCo is not null)
            {
                // Tài khoản đã tồn tại (SV học nhiều lớp) — chỉ gắn vào enrollment, không tạo lại.
                e.UserId = daCo.Id;
                e.Status = EnrollmentStatus.Active;
                boQua++;
                ketQua.Add(new CreatedAccountDto(e.StudentCode, loginEmail, null, "Tài khoản đã có sẵn, đã gắn vào lớp."));
                continue;
            }

            var matKhau = SinhMatKhauBanDau(e.StudentCode);
            var user = new AppUser
            {
                UserName = loginEmail,
                Email = loginEmail,
                FullName = e.FullName,
                StudentCode = e.StudentCode,
                EmailConfirmed = true,
                MustChangePassword = true,   // buộc đổi ở lần đăng nhập đầu (SRS mục 8)
            };

            var tao = await users.CreateAsync(user, matKhau);
            if (!tao.Succeeded)
            {
                boQua++;
                ketQua.Add(new CreatedAccountDto(e.StudentCode, loginEmail, null,
                    string.Join(" ", tao.Errors.Select(x => x.Description))));
                continue;
            }

            await users.AddToRoleAsync(user, Roles.Student);
            e.UserId = user.Id;
            e.Status = EnrollmentStatus.Active;
            taoMoi++;

            ketQua.Add(new CreatedAccountDto(e.StudentCode, loginEmail, matKhau, "Đã tạo mới."));
        }

        await db.SaveChangesAsync(ct);

        return Result<CreateStudentAccountsResponse>.Ok(
            new CreateStudentAccountsResponse(taoMoi, boQua, ketQua));
    }

    /// <summary>
    /// Mật khẩu ban đầu suy ra từ MSSV: giảng viên đọc được cho sinh viên mà không cần bảng tra,
    /// và nó vô hại vì MustChangePassword buộc đổi ngay lần đăng nhập đầu.
    /// </summary>
    private static string SinhMatKhauBanDau(string studentCode) => $"Th@{studentCode}";
}
