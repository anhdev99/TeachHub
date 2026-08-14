using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Subjects.Commands.UpdateVersion;

public sealed record UpdateVersionCommand(
    string? VersionLabel = null,
    string? Changelog = null,
    Syllabus? Syllabus = null) : ICommand<Result<SubjectVersionDto>>
{
    public Guid VersionId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UpdateVersionCommandValidator : AbstractValidator<UpdateVersionCommand>
{
    public UpdateVersionCommandValidator()
    {
        RuleFor(x => x.VersionLabel)
            .MaximumLength(50).WithMessage("Nhãn phiên bản dài quá 50 ký tự.");
        RuleFor(x => x.Changelog)
            .MaximumLength(4000).WithMessage("Ghi chú thay đổi dài quá 4000 ký tự.");
    }
}

public sealed class UpdateVersionCommandHandler(IAppDbContext db)
    : ICommandHandler<UpdateVersionCommand, Result<SubjectVersionDto>>
{
    public async Task<Result<SubjectVersionDto>> Handle(UpdateVersionCommand request, CancellationToken ct)
    {
        var version = await db.SubjectVersions.SingleOrDefaultAsync(v => v.Id == request.VersionId, ct);
        if (version is null) return Result<SubjectVersionDto>.NotFound("Không tìm thấy phiên bản.");

        var laCuaMinh = await db.Subjects.AnyAsync(
            s => s.Id == version.SubjectId && s.InstructorId == request.InstructorId, ct);
        if (!laCuaMinh) return Result<SubjectVersionDto>.NotFound("Không tìm thấy phiên bản.");

        // Bất biến cốt lõi: chỉ bản nháp mới sửa được. Đây là thứ giữ cho các lớp đang dạy
        // không bị đổi nội dung dưới chân (SRS mục 5.2).
        if (!version.CanEditContent)
            return Result<SubjectVersionDto>.Conflict(
                $"Phiên bản đang ở trạng thái {version.Status}, chỉ bản nháp mới sửa được.");

        if (request.Changelog is not null) version.Changelog = request.Changelog;
        if (request.Syllabus is not null) version.Syllabus = request.Syllabus;

        await db.SaveChangesAsync(ct);
        return Result<SubjectVersionDto>.Ok(SubjectVersionDto.From(version));
    }
}
