using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Assignments;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Assessments;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Assessments;

public sealed record FinalAssessmentDto(
    Guid Id, Guid OfferingId, FinalAssessmentType Type, string Title, string? Description,
    Guid? GradeColumnId, DateTimeOffset? DueAt, SubmissionMode SubmissionMode,
    Guid? RubricId, decimal MaxScore, List<SubmissionSlotDto> Slots)
{
    public static FinalAssessmentDto From(FinalAssessment f) => new(
        f.Id, f.OfferingId, f.Type, f.Title, f.Description, f.GradeColumnId, f.DueAt,
        f.SubmissionMode, f.RubricId, f.MaxScore,
        [.. f.Slots.OrderBy(s => s.OrderIndex).Select(SubmissionSlotDto.From)]);
}

// ── Tạo đợt cuối môn ────────────────────────────────────────────────────────────────────

public sealed record CreateFinalAssessmentCommand(
    FinalAssessmentType Type, string Title, string? Description,
    Guid? GradeColumnId, DateTimeOffset? DueAt, SubmissionMode SubmissionMode,
    decimal MaxScore, bool UseSuggestedSlots) : ICommand<Result<FinalAssessmentDto>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CreateFinalAssessmentValidator : AbstractValidator<CreateFinalAssessmentCommand>
{
    public CreateFinalAssessmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Chưa nhập tên đợt.").MaximumLength(200);
        RuleFor(x => x.MaxScore).InclusiveBetween(0m, 10m)
            .WithMessage("Điểm tối đa phải nằm trong khoảng 0 đến 10.");

        // "exam" KHÔNG có trong enum nên JsonStringEnumConverter chặn ngay ở tầng đọc JSON
        // với thông báo của bộ đọc. "quiz" CÓ trong enum nên nó đi tới được đây — đây là chỗ
        // duy nhất kiểm chứng được câu "chưa hỗ trợ" do mình viết ra.
        RuleFor(x => x.Type)
            .Must(t => t == FinalAssessmentType.FinalProject)
            .WithMessage("Loại này chưa hỗ trợ trong MVP — chỉ có bài tập lớn cuối kỳ. "
                       + "Trắc nghiệm chờ engine quiz ở Phase 5.");
    }
}

public sealed class CreateFinalAssessmentHandler(IAppDbContext db)
    : ICommandHandler<CreateFinalAssessmentCommand, Result<FinalAssessmentDto>>
{
    public async Task<Result<FinalAssessmentDto>> Handle(
        CreateFinalAssessmentCommand request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<FinalAssessmentDto>.NotFound("Không tìm thấy lớp học phần.");

        var dot = new FinalAssessment
        {
            OfferingId = request.OfferingId,
            Type = request.Type,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            GradeColumnId = request.GradeColumnId,
            DueAt = request.DueAt,
            SubmissionMode = request.SubmissionMode,
            MaxScore = request.MaxScore,
        };

        // Dùng lại NGUYÊN VẸN mẫu slot của Phase 3 — chỉ khác owner. Viết một bộ mẫu riêng
        // cho bài tập lớn là dựng hệ thống thứ hai, và hai hệ thống sẽ lệch nhau sau vài lần sửa.
        if (request.UseSuggestedSlots)
            foreach (var s in SlotTemplates.Suggested())
            {
                s.OwnerType = SlotOwnerType.FinalAssessment;
                s.AssignmentId = null;
                s.FinalAssessmentId = dot.Id;
                dot.Slots.Add(s);
            }

        db.FinalAssessments.Add(dot);
        await db.SaveChangesAsync(ct);

        return Result<FinalAssessmentDto>.Ok(FinalAssessmentDto.From(dot));
    }
}

// ── Đọc danh sách ───────────────────────────────────────────────────────────────────────

public sealed record GetFinalAssessmentsQuery(Guid OfferingId, Guid ActorId)
    : IQuery<Result<List<FinalAssessmentDto>>>;

public sealed class GetFinalAssessmentsHandler(IAppDbContext db)
    : IQueryHandler<GetFinalAssessmentsQuery, Result<List<FinalAssessmentDto>>>
{
    public async Task<Result<List<FinalAssessmentDto>>> Handle(
        GetFinalAssessmentsQuery request, CancellationToken ct)
    {
        var laGiangVien = await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.ActorId, ct);
        var laSinhVien = await db.Enrollments.AnyAsync(
            e => e.OfferingId == request.OfferingId && e.UserId == request.ActorId, ct);

        if (!laGiangVien && !laSinhVien)
            return Result<List<FinalAssessmentDto>>.NotFound("Không tìm thấy lớp học phần.");

        var ds = await db.FinalAssessments.AsNoTracking()
            .Include(f => f.Slots)
            .Where(f => f.OfferingId == request.OfferingId)
            .OrderBy(f => f.DueAt ?? DateTimeOffset.MaxValue).ThenBy(f => f.Title)
            .ToListAsync(ct);

        return Result<List<FinalAssessmentDto>>.Ok([.. ds.Select(FinalAssessmentDto.From)]);
    }
}

// ── Thêm slot cho đợt cuối môn ──────────────────────────────────────────────────────────

public sealed record AddFinalAssessmentSlotCommand(
    string SlotKey, string Label, List<string> AcceptedFileTypes,
    bool AllowLinkInsteadOfFile, int MaxFiles, int MaxFileSizeMb, bool IsRequired,
    List<string> RequiredFiles, List<string> RequiredFolders) : ICommand<Result<SubmissionSlotDto>>
{
    public Guid FinalAssessmentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class AddFinalAssessmentSlotValidator : AbstractValidator<AddFinalAssessmentSlotCommand>
{
    public AddFinalAssessmentSlotValidator()
    {
        RuleFor(x => x.SlotKey).NotEmpty().MaximumLength(64).Matches("^[a-z0-9_]+$")
            .WithMessage("Khóa slot chỉ gồm chữ thường, số và dấu gạch dưới.");
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaxFiles).InclusiveBetween(1, 50);
        RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 500);
        RuleFor(x => x.AcceptedFileTypes).NotEmpty();
    }
}

public sealed class AddFinalAssessmentSlotHandler(IAppDbContext db)
    : ICommandHandler<AddFinalAssessmentSlotCommand, Result<SubmissionSlotDto>>
{
    public async Task<Result<SubmissionSlotDto>> Handle(
        AddFinalAssessmentSlotCommand request, CancellationToken ct)
    {
        var dot = await db.FinalAssessments
            .SingleOrDefaultAsync(f => f.Id == request.FinalAssessmentId, ct);

        if (dot is null ||
            !await OfferingOwnership.OwnsOfferingAsync(db, dot.OfferingId, request.InstructorId, ct))
            return Result<SubmissionSlotDto>.NotFound("Không tìm thấy đợt chấm cuối môn.");

        var key = request.SlotKey.Trim().ToLowerInvariant();

        if (await db.SubmissionSlots.AnyAsync(
                s => s.FinalAssessmentId == dot.Id && s.SlotKey == key, ct))
            return Result<SubmissionSlotDto>.Conflict($"Đợt này đã có slot “{key}”.");

        var thuTu = await db.SubmissionSlots.Where(s => s.FinalAssessmentId == dot.Id)
            .Select(s => (int?)s.OrderIndex).MaxAsync(ct) ?? -1;

        var slot = new SubmissionSlot
        {
            OwnerType = SlotOwnerType.FinalAssessment,
            FinalAssessmentId = dot.Id,
            OrderIndex = thuTu + 1,
            SlotKey = key,
            Label = request.Label.Trim(),
            AcceptedFileTypes = [.. request.AcceptedFileTypes.Select(t => t.Trim().ToLowerInvariant())],
            AllowLinkInsteadOfFile = request.AllowLinkInsteadOfFile,
            MaxFiles = request.MaxFiles,
            MaxFileSizeMb = request.MaxFileSizeMb,
            IsRequired = request.IsRequired,
            RequiredStructure = request.RequiredFiles.Count == 0 && request.RequiredFolders.Count == 0
                ? null
                : new RequiredStructure
                {
                    RequiredFiles = [.. request.RequiredFiles],
                    RequiredFolders = [.. request.RequiredFolders],
                },
        };

        db.SubmissionSlots.Add(slot);
        await db.SaveChangesAsync(ct);

        return Result<SubmissionSlotDto>.Ok(SubmissionSlotDto.From(slot));
    }
}
