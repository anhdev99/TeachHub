using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Assignments;

namespace TeachHub.Application.Features.Assignments.Commands.ManageSlots;

// ── Thêm slot ───────────────────────────────────────────────────────────────────────────

public sealed record AddSubmissionSlotCommand(
    string SlotKey, string Label, List<string> AcceptedFileTypes,
    bool AllowLinkInsteadOfFile, int MaxFiles, int MaxFileSizeMb, bool IsRequired,
    List<string> RequiredFiles, List<string> RequiredFolders) : ICommand<Result<SubmissionSlotDto>>
{
    public Guid AssignmentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class AddSubmissionSlotValidator : AbstractValidator<AddSubmissionSlotCommand>
{
    public AddSubmissionSlotValidator()
    {
        RuleFor(x => x.SlotKey).NotEmpty().WithMessage("Chưa nhập khóa slot.")
            .MaximumLength(64)
            .Matches("^[a-z0-9_]+$")
            .WithMessage("Khóa slot chỉ gồm chữ thường, số và dấu gạch dưới.");
        RuleFor(x => x.Label).NotEmpty().WithMessage("Chưa nhập nhãn hiển thị.").MaximumLength(200);
        RuleFor(x => x.MaxFiles).InclusiveBetween(1, 50)
            .WithMessage("Số tệp tối đa phải nằm trong khoảng 1 đến 50.");
        RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 500)
            .WithMessage("Dung lượng tối đa phải nằm trong khoảng 1 đến 500 MB.");
        RuleFor(x => x.AcceptedFileTypes).NotEmpty()
            .WithMessage("Phải khai ít nhất một định dạng được nhận.");
        RuleForEach(x => x.AcceptedFileTypes).Matches(@"^\.[A-Za-z0-9]+$")
            .WithMessage("Định dạng phải viết dạng '.zip', '.pdf'.");
    }
}

public sealed class AddSubmissionSlotHandler(IAppDbContext db)
    : ICommandHandler<AddSubmissionSlotCommand, Result<SubmissionSlotDto>>
{
    public async Task<Result<SubmissionSlotDto>> Handle(AddSubmissionSlotCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsAssignmentAsync(db, request.AssignmentId, request.InstructorId, ct))
            return Result<SubmissionSlotDto>.NotFound("Không tìm thấy bài tập.");

        var key = request.SlotKey.Trim().ToLowerInvariant();

        if (await db.SubmissionSlots.AnyAsync(s => s.AssignmentId == request.AssignmentId && s.SlotKey == key, ct))
            return Result<SubmissionSlotDto>.Conflict($"Bài tập này đã có slot “{key}”.");

        var thuTuCuoi = await db.SubmissionSlots
            .Where(s => s.AssignmentId == request.AssignmentId)
            .Select(s => (int?)s.OrderIndex).MaxAsync(ct) ?? -1;

        var slot = new SubmissionSlot
        {
            AssignmentId = request.AssignmentId,
            OwnerType = SlotOwnerType.Assignment,
            OrderIndex = thuTuCuoi + 1,
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

// ── Sửa slot ────────────────────────────────────────────────────────────────────────────

public sealed record UpdateSubmissionSlotCommand(
    string Label, List<string> AcceptedFileTypes,
    bool AllowLinkInsteadOfFile, int MaxFiles, int MaxFileSizeMb, bool IsRequired,
    List<string> RequiredFiles, List<string> RequiredFolders) : ICommand<Result<SubmissionSlotDto>>
{
    public Guid SlotId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class UpdateSubmissionSlotValidator : AbstractValidator<UpdateSubmissionSlotCommand>
{
    public UpdateSubmissionSlotValidator()
    {
        RuleFor(x => x.Label).NotEmpty().WithMessage("Chưa nhập nhãn hiển thị.").MaximumLength(200);
        RuleFor(x => x.MaxFiles).InclusiveBetween(1, 50)
            .WithMessage("Số tệp tối đa phải nằm trong khoảng 1 đến 50.");
        RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 500)
            .WithMessage("Dung lượng tối đa phải nằm trong khoảng 1 đến 500 MB.");
        RuleFor(x => x.AcceptedFileTypes).NotEmpty()
            .WithMessage("Phải khai ít nhất một định dạng được nhận.");
    }
}

public sealed class UpdateSubmissionSlotHandler(IAppDbContext db)
    : ICommandHandler<UpdateSubmissionSlotCommand, Result<SubmissionSlotDto>>
{
    public async Task<Result<SubmissionSlotDto>> Handle(UpdateSubmissionSlotCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsSlotAsync(db, request.SlotId, request.InstructorId, ct))
            return Result<SubmissionSlotDto>.NotFound("Không tìm thấy slot nộp bài.");

        var slot = await db.SubmissionSlots.SingleAsync(s => s.Id == request.SlotId, ct);

        slot.Label = request.Label.Trim();
        slot.AcceptedFileTypes = [.. request.AcceptedFileTypes.Select(t => t.Trim().ToLowerInvariant())];
        slot.AllowLinkInsteadOfFile = request.AllowLinkInsteadOfFile;
        slot.MaxFiles = request.MaxFiles;
        slot.MaxFileSizeMb = request.MaxFileSizeMb;
        slot.IsRequired = request.IsRequired;
        slot.RequiredStructure = request.RequiredFiles.Count == 0 && request.RequiredFolders.Count == 0
            ? null
            : new RequiredStructure
            {
                RequiredFiles = [.. request.RequiredFiles],
                RequiredFolders = [.. request.RequiredFolders],
            };

        await db.SaveChangesAsync(ct);

        return Result<SubmissionSlotDto>.Ok(SubmissionSlotDto.From(slot));
    }
}

// ── Xóa slot ────────────────────────────────────────────────────────────────────────────

public sealed record DeleteSubmissionSlotCommand : ICommand<Result>
{
    public Guid SlotId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class DeleteSubmissionSlotHandler(IAppDbContext db)
    : ICommandHandler<DeleteSubmissionSlotCommand, Result>
{
    public async Task<Result> Handle(DeleteSubmissionSlotCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsSlotAsync(db, request.SlotId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy slot nộp bài.");

        // Slot đã có bài nộp mà biến mất là mất bằng chứng chấm điểm. Trả 409 kèm con số
        // để giảng viên biết mình đang định xóa cái gì, thay vì xóa xếp tầng trong im lặng.
        var soFile = await db.SubmissionFiles.CountAsync(f => f.SlotId == request.SlotId, ct);
        if (soFile > 0)
            return Result.Conflict(
                $"Slot này đã có {soFile} tệp sinh viên nộp. Xóa slot sẽ mất bằng chứng chấm điểm.");

        var slot = await db.SubmissionSlots.SingleAsync(s => s.Id == request.SlotId, ct);
        db.SubmissionSlots.Remove(slot);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}

// ── Dùng mẫu gợi ý ──────────────────────────────────────────────────────────────────────

public sealed record ApplySuggestedSlotsCommand : ICommand<Result<List<SubmissionSlotDto>>>
{
    public Guid AssignmentId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ApplySuggestedSlotsHandler(IAppDbContext db)
    : ICommandHandler<ApplySuggestedSlotsCommand, Result<List<SubmissionSlotDto>>>
{
    public async Task<Result<List<SubmissionSlotDto>>> Handle(
        ApplySuggestedSlotsCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsAssignmentAsync(db, request.AssignmentId, request.InstructorId, ct))
            return Result<List<SubmissionSlotDto>>.NotFound("Không tìm thấy bài tập.");

        var daCo = await db.SubmissionSlots
            .Where(s => s.AssignmentId == request.AssignmentId)
            .Select(s => s.SlotKey)
            .ToListAsync(ct);

        var thuTu = daCo.Count == 0
            ? -1
            : await db.SubmissionSlots.Where(s => s.AssignmentId == request.AssignmentId)
                                      .Select(s => s.OrderIndex).MaxAsync(ct);

        var them = new List<SubmissionSlot>();

        // Bỏ qua slot đã có thay vì báo lỗi: bấm "Dùng mẫu gợi ý" lần thứ hai là thao tác
        // vô hại của người dùng, không phải lỗi cần chặn.
        foreach (var s in SlotTemplates.Suggested().Where(s => !daCo.Contains(s.SlotKey)))
        {
            s.AssignmentId = request.AssignmentId;
            s.OrderIndex = ++thuTu;
            them.Add(s);
        }

        if (them.Count > 0)
        {
            db.SubmissionSlots.AddRange(them);
            await db.SaveChangesAsync(ct);
        }

        var tatCa = await db.SubmissionSlots.AsNoTracking()
            .Where(s => s.AssignmentId == request.AssignmentId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(ct);

        return Result<List<SubmissionSlotDto>>.Ok([.. tatCa.Select(SubmissionSlotDto.From)]);
    }
}
