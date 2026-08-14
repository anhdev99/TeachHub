using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Subjects.Commands.CreateSubject;

public sealed record CreateSubjectCommand(string Code, string Name, string? Description = null)
    : ICommand<Result<SubjectDto>>
{
    // Trường server tự điền: property init-only, KHÔNG phải tham số positional có `= default`.
    // Tham số positional kiểu giá trị mang giá trị mặc định làm JsonSchemaExporter ném
    // JsonException → /openapi/v1.json trả 500 → pnpm gen:api chết.
    public Guid InstructorId { get; init; }
}

public sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Chưa nhập mã môn.")
            .MaximumLength(20).WithMessage("Mã môn dài quá 20 ký tự.")
            .Matches("^[A-Z0-9]+$").WithMessage("Mã môn chỉ gồm chữ in hoa và số.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Chưa nhập tên môn.")
            .MaximumLength(300).WithMessage("Tên môn dài quá 300 ký tự.");
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Mô tả dài quá 2000 ký tự.");
    }
}

public sealed class CreateSubjectCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<CreateSubjectCommand, Result<SubjectDto>>
{
    private const string NhanMacDinh = "Bản đầu tiên";

    public async Task<Result<SubjectDto>> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        var trung = await db.Subjects.AnyAsync(
            s => s.InstructorId == request.InstructorId && s.Code == request.Code, ct);

        if (trung) return Result<SubjectDto>.Conflict($"Bạn đã có môn mang mã '{request.Code}'.");

        var subject = new Subject
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            InstructorId = request.InstructorId,
        };

        // Tạo luôn một phiên bản draft đầu tiên: môn học không có phiên bản nào thì không
        // soạn được nội dung, và bắt người dùng bấm thêm một nút nữa chỉ để tới được chỗ
        // duy nhất họ có thể đi tiếp là thủ tục thừa.
        var version = SubjectVersion.CreateDraft(subject.Id, NhanMacDinh, clock.GetUtcNow());

        db.Subjects.Add(subject);
        db.SubjectVersions.Add(version);
        await db.SaveChangesAsync(ct);

        return Result<SubjectDto>.Ok(SubjectDto.From(subject));
    }
}
