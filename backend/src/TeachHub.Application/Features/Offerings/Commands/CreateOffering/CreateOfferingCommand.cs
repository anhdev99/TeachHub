using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings.Commands.CreateOffering;

public sealed record CreateOfferingCommand(
    Guid SubjectId,
    string Semester,
    string ClassCode,
    Guid? SubjectVersionId = null,
    string? TimeZoneId = null) : ICommand<Result<OfferingDto>>
{
    public Guid InstructorId { get; init; }
}

public sealed class CreateOfferingCommandValidator : AbstractValidator<CreateOfferingCommand>
{
    // KHÔNG nhận IAppDbContext. Validator là hàm thuần: luật nào cần đọc database thì handler
    // làm và trả Result.Invalid(...). Cho validator chạm I/O là mở một round-trip TRƯỚC
    // TransactionBehavior, ngoài transaction — rồi handler đọc lại bên trong và có thể ra
    // kết quả khác.
    public CreateOfferingCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty().WithMessage("Chưa chọn môn học.");
        RuleFor(x => x.Semester)
            .NotEmpty().WithMessage("Chưa nhập học kỳ.")
            .MaximumLength(50).WithMessage("Học kỳ dài quá 50 ký tự.");
        RuleFor(x => x.ClassCode)
            .NotEmpty().WithMessage("Chưa nhập mã lớp.")
            .MaximumLength(50).WithMessage("Mã lớp dài quá 50 ký tự.");
    }
}

public sealed class CreateOfferingCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CreateOfferingCommand, Result<OfferingDto>>
{
    private const int SoNgayMaLopSong = 30;

    public async Task<Result<OfferingDto>> Handle(CreateOfferingCommand request, CancellationToken ct)
    {
        var subject = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(
            s => s.Id == request.SubjectId && s.InstructorId == request.InstructorId, ct);

        if (subject is null) return Result<OfferingDto>.NotFound("Không tìm thấy môn học.");

        var versionId = request.SubjectVersionId ?? subject.CurrentVersionId;
        if (versionId is null)
            return Result<OfferingDto>.Invalid("Môn học chưa có phiên bản nào được xuất bản.");

        var version = await db.SubjectVersions.AsNoTracking()
            .Where(v => v.Id == versionId && v.SubjectId == subject.Id)
            .Select(v => new { v.Status })
            .SingleOrDefaultAsync(ct);

        if (version is null) return Result<OfferingDto>.NotFound("Không tìm thấy phiên bản môn học.");

        // Mở lớp từ bản nháp nghĩa là nội dung lớp sẽ đổi dưới chân sinh viên mỗi lần
        // giảng viên sửa một dòng — đúng thứ mà cơ chế versioning sinh ra để chặn.
        if (version.Status == SubjectVersionStatus.Draft)
            return Result<OfferingDto>.Invalid("Không mở được lớp từ phiên bản đang ở trạng thái nháp.");

        var trung = await db.CourseOfferings.AnyAsync(
            o => o.SubjectId == subject.Id && o.Semester == request.Semester
                 && o.ClassCode == request.ClassCode, ct);
        if (trung)
            return Result<OfferingDto>.Conflict($"Đã có lớp '{request.ClassCode}' trong học kỳ này.");

        var now = clock.GetUtcNow();
        var offering = new CourseOffering
        {
            SubjectId = subject.Id,
            SubjectVersionId = versionId.Value,   // GHIM CỨNG, init-only
            Semester = request.Semester,
            ClassCode = request.ClassCode,
            Status = OfferingStatus.Active,
            JoinCodeValue = await SinhMaKhongTrungAsync(ct),
            JoinCodeExpiresAt = now.AddDays(SoNgayMaLopSong),
            TimeZoneId = request.TimeZoneId ?? "Asia/Ho_Chi_Minh",
        };

        db.CourseOfferings.Add(offering);
        await db.SaveChangesAsync(ct);

        return Result<OfferingDto>.Ok(new OfferingDto(
            offering.Id, offering.SubjectId, offering.SubjectVersionId,
            subject.Code, subject.Name, offering.Semester,
            offering.ClassCode, offering.JoinCodeValue, offering.JoinCodeExpiresAt,
            offering.Status, offering.TimeZoneId, 0, offering.CreatedAt));
    }

    /// <summary>
    /// Có unique index trên join_code_value, nên phải thử lại khi trùng. Không gian mã là
    /// 31^6 ≈ 887 triệu; va chạm cực hiếm nhưng "cực hiếm" không phải "không bao giờ",
    /// và lần va chạm đó sẽ rơi đúng vào lúc mở lớp đầu học kỳ.
    /// </summary>
    private async Task<string> SinhMaKhongTrungAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var ma = JoinCode.Generate();
            if (!await db.CourseOfferings.AnyAsync(o => o.JoinCodeValue == ma, ct)) return ma;
        }

        throw new InvalidOperationException("Không sinh được mã lớp duy nhất sau 10 lần thử.");
    }
}
