using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Identity;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings.Commands.JoinOffering;

public sealed record JoinOfferingCommand(string JoinCode) : ICommand<Result>
{
    public Guid UserId { get; init; }
}

public sealed class JoinOfferingCommandValidator : AbstractValidator<JoinOfferingCommand>
{
    public JoinOfferingCommandValidator() =>
        RuleFor(x => x.JoinCode).NotEmpty().WithMessage("Chưa nhập mã lớp.");
}

public sealed class JoinOfferingCommandHandler(
    IAppDbContext db,
    UserManager<AppUser> users,
    TimeProvider clock) : ICommandHandler<JoinOfferingCommand, Result>
{
    // MỘT thông báo duy nhất cho mọi lý do thất bại. Mã lớp sẽ bị lan ra ngoài — đó là chuyện
    // chắc chắn xảy ra. Nếu "mã sai" và "mã đúng nhưng không có tên trong danh sách" trả về
    // khác nhau, kẻ dò mã phân biệt được hai ca đó và dò ra mã hợp lệ chỉ bằng cách thử.
    private const string LoiChung = "Mã lớp không hợp lệ, đã hết hạn, hoặc bạn không có tên trong danh sách lớp.";

    public async Task<Result> Handle(JoinOfferingCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ma = request.JoinCode.Trim();

        var offering = await db.CourseOfferings
            .SingleOrDefaultAsync(o => o.JoinCodeValue == ma, ct);

        if (offering is null || !offering.AcceptsJoinCode(ma, now)) return Result.Forbidden(LoiChung);

        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) return Result.Forbidden(LoiChung);

        // Mã đúng vẫn CHƯA đủ: phải có tên trong danh sách do phòng đào tạo gửi.
        // Ghép theo MSSV trước, rồi tới email — SV import từ Excel có thể thiếu một trong hai.
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(
            e => e.OfferingId == offering.Id &&
                 ((user.StudentCode != null && e.StudentCode == user.StudentCode) ||
                  (user.Email != null && e.Email == user.Email)), ct);

        if (enrollment is null) return Result.Forbidden(LoiChung);

        // Ghế này đã thuộc về một tài khoản khác ⇒ có người mạo danh, hoặc dữ liệu import sai.
        if (enrollment.UserId is not null && enrollment.UserId != user.Id)
            return Result.Forbidden(LoiChung);

        enrollment.UserId = user.Id;
        enrollment.Status = EnrollmentStatus.Active;
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
