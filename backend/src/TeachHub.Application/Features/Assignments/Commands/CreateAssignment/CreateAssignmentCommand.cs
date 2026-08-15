using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Content;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Assignments.Commands.CreateAssignment;

public sealed record CreateAssignmentCommand(
    string Title,
    string? Description,
    DateTimeOffset? DueAt,
    bool AllowLateSubmission,
    decimal LatePenaltyPercentPerDay,
    decimal MaxScore,
    bool UseSuggestedSlots) : ICommand<Result<AssignmentDto>>
{
    // Trường server tự điền là init-only property, KHÔNG phải tham số positional mang
    // giá trị mặc định: positional + `= default` làm JsonSchemaExporter ném exception và
    // /openapi/v1.json trả 500, kéo theo pnpm gen:api chết.
    public Guid SubjectVersionId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CreateAssignmentValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Chưa nhập tên bài tập.").MaximumLength(300);
        RuleFor(x => x.MaxScore).InclusiveBetween(0m, 10m)
            .WithMessage("Điểm tối đa phải nằm trong khoảng 0 đến 10.");
        RuleFor(x => x.LatePenaltyPercentPerDay).InclusiveBetween(0m, 100m)
            .WithMessage("Phần trăm trừ điểm mỗi ngày trễ phải nằm trong khoảng 0 đến 100.");
    }
}

public sealed class CreateAssignmentHandler(IAppDbContext db)
    : ICommandHandler<CreateAssignmentCommand, Result<AssignmentDto>>
{
    public async Task<Result<AssignmentDto>> Handle(CreateAssignmentCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsVersionAsync(db, request.SubjectVersionId, request.InstructorId, ct))
            return Result<AssignmentDto>.NotFound("Không tìm thấy phiên bản môn học.");

        var status = await db.SubjectVersions.AsNoTracking()
            .Where(v => v.Id == request.SubjectVersionId)
            .Select(v => v.Status)
            .SingleAsync(ct);

        // Chỉ chặn bản LƯU TRỮ, không chặn bản đã xuất bản — khác với chương/bài học.
        //
        // Bài học là thứ sinh viên đang đọc: sửa giữa học kỳ là đổi nội dung dưới chân người
        // đang học, nên Phase 1 gắn RequireDraftVersion cho mọi thao tác sửa bài học. Bài tập
        // thì ngược lại: giao thêm bài giữa học kỳ là việc bình thường nhất của một giảng viên,
        // và nó THÊM chứ không sửa thứ gì sinh viên đã học. Chặn ở đây nghĩa là muốn ra thêm
        // một bài tập phải tạo phiên bản môn mới rồi mở lại lớp — không ai làm vậy giữa kỳ.
        //
        // Phần nguy hiểm thật (xóa slot khi đã có bài nộp) đã được chặn riêng bằng 409.
        if (status == SubjectVersionStatus.Archived)
            return Result<AssignmentDto>.Conflict(
                "Phiên bản này đã lưu trữ, không thêm bài tập được nữa.");

        var assignment = new Assignment
        {
            SubjectVersionId = request.SubjectVersionId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DueAt = request.DueAt,
            AllowLateSubmission = request.AllowLateSubmission,
            LatePenaltyPercentPerDay = request.LatePenaltyPercentPerDay,
            MaxScore = request.MaxScore,
        };

        if (request.UseSuggestedSlots)
            foreach (var s in SlotTemplates.Suggested())
            {
                s.AssignmentId = assignment.Id;
                assignment.Slots.Add(s);
            }

        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        return Result<AssignmentDto>.Ok(AssignmentDto.From(assignment));
    }
}
