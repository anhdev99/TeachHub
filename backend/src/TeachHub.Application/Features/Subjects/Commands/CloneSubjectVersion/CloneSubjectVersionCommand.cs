using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Subjects.Commands.CloneSubjectVersion;

// SubjectId và InstructorId là property init-only, KHÔNG phải tham số positional có giá trị mặc định.
// Lý do không phải thẩm mỹ: một tham số record kiểu giá trị mang `= default` làm JsonSchemaExporter
// của Microsoft.AspNetCore.OpenApi ném JsonException khi sinh schema, và GET /openapi/v1.json
// trả 500 — kéo theo `pnpm gen:api` không chạy được.
public sealed record CloneSubjectVersionCommand(
    Guid? ClonedFromVersionId,
    string VersionLabel) : ICommand<Result<SubjectVersionDto>>
{
    public Guid SubjectId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CloneSubjectVersionCommandValidator : AbstractValidator<CloneSubjectVersionCommand>
{
    public CloneSubjectVersionCommandValidator()
    {
        RuleFor(x => x.VersionLabel)
            .NotEmpty().WithMessage("Chưa nhập nhãn phiên bản.")
            .MaximumLength(50).WithMessage("Nhãn phiên bản dài quá 50 ký tự.");
    }
}

public sealed class CloneSubjectVersionCommandHandler(IAppDbContext db, SubjectVersionCloner cloner)
    : ICommandHandler<CloneSubjectVersionCommand, Result<SubjectVersionDto>>
{
    public async Task<Result<SubjectVersionDto>> Handle(
        CloneSubjectVersionCommand request, CancellationToken ct)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(
            s => s.Id == request.SubjectId && s.InstructorId == request.InstructorId, ct);

        if (subject is null) return Result<SubjectVersionDto>.NotFound("Không tìm thấy môn học.");

        var nguonId = request.ClonedFromVersionId ?? subject.CurrentVersionId;
        if (nguonId is null)
            return Result<SubjectVersionDto>.Conflict("Môn học chưa có phiên bản nào để nhân bản.");

        // Phiên bản nguồn phải thuộc chính môn này — nếu không, ai đó truyền id phiên bản
        // của giảng viên khác vào và sao chép được nội dung của họ.
        var nguonThuocMon = await db.SubjectVersions.AnyAsync(
            v => v.Id == nguonId && v.SubjectId == subject.Id, ct);
        if (!nguonThuocMon)
            return Result<SubjectVersionDto>.NotFound("Không tìm thấy phiên bản nguồn.");

        var trungNhan = await db.SubjectVersions.AnyAsync(
            v => v.SubjectId == subject.Id && v.VersionLabel == request.VersionLabel, ct);
        if (trungNhan)
            return Result<SubjectVersionDto>.Conflict($"Môn học đã có phiên bản '{request.VersionLabel}'.");

        // KHÔNG có BeginTransactionAsync ở đây: CloneSubjectVersionCommand là ICommand nên
        // TransactionBehavior đã bọc sẵn. Tự mở transaction là mở lồng và làm Commit mất nghĩa.
        var moi = await cloner.CloneAsync(nguonId.Value, request.VersionLabel, ct);
        return Result<SubjectVersionDto>.Ok(SubjectVersionDto.From(moi));
    }
}
