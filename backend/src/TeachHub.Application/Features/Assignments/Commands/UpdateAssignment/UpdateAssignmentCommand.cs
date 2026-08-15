using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Assignments.Commands.UpdateAssignment;

public sealed record UpdateAssignmentCommand(
    string Title,
    string? Description,
    DateTimeOffset? DueAt,
    bool AllowLateSubmission,
    decimal LatePenaltyPercentPerDay,
    decimal MaxScore,
    Guid? RubricId) : ICommand<Result<AssignmentDto>>
{
    public Guid AssignmentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Chưa nhập tên bài tập.").MaximumLength(300);
        RuleFor(x => x.MaxScore).InclusiveBetween(0m, 10m)
            .WithMessage("Điểm tối đa phải nằm trong khoảng 0 đến 10.");
        RuleFor(x => x.LatePenaltyPercentPerDay).InclusiveBetween(0m, 100m)
            .WithMessage("Phần trăm trừ điểm mỗi ngày trễ phải nằm trong khoảng 0 đến 100.");
    }
}

public sealed class UpdateAssignmentHandler(IAppDbContext db)
    : ICommandHandler<UpdateAssignmentCommand, Result<AssignmentDto>>
{
    public async Task<Result<AssignmentDto>> Handle(UpdateAssignmentCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsAssignmentAsync(db, request.AssignmentId, request.InstructorId, ct))
            return Result<AssignmentDto>.NotFound("Không tìm thấy bài tập.");

        var a = await db.Assignments
            .Include(x => x.Slots)
            .Include(x => x.Hints)
            .SingleAsync(x => x.Id == request.AssignmentId, ct);

        // Rubric phải thuộc chính giảng viên này. Không kiểm thì gắn được rubric của
        // đồng nghiệp vào bài của mình và đọc được toàn bộ tiêu chí chấm của họ.
        if (request.RubricId is not null &&
            !await db.Rubrics.AnyAsync(r => r.Id == request.RubricId && r.InstructorId == request.InstructorId, ct))
            return Result<AssignmentDto>.NotFound("Không tìm thấy rubric.");

        a.Title = request.Title.Trim();
        a.Description = request.Description?.Trim();
        a.DueAt = request.DueAt;
        a.AllowLateSubmission = request.AllowLateSubmission;
        a.LatePenaltyPercentPerDay = request.LatePenaltyPercentPerDay;
        a.MaxScore = request.MaxScore;
        a.RubricId = request.RubricId;

        await db.SaveChangesAsync(ct);

        return Result<AssignmentDto>.Ok(AssignmentDto.From(a));
    }
}
