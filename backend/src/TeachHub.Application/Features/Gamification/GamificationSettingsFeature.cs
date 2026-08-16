using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Gamification;

namespace TeachHub.Application.Features.Gamification;

// ── Đọc cấu hình của lớp ────────────────────────────────────────────────────────────────

public sealed record GetGamificationSettingsQuery(Guid OfferingId, Guid InstructorId)
    : IQuery<Result<GamificationSettingsDto>>;

public sealed class GetGamificationSettingsHandler(IAppDbContext db)
    : IQueryHandler<GetGamificationSettingsQuery, Result<GamificationSettingsDto>>
{
    public async Task<Result<GamificationSettingsDto>> Handle(
        GetGamificationSettingsQuery request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<GamificationSettingsDto>.NotFound("Không tìm thấy lớp học phần.");

        // Lớp chưa cấu hình gì trả về bản MẶC ĐỊNH (ẩn danh) chứ không phải 404: màn hình cấu
        // hình cần có gì đó để hiển thị, và mặc định phải hiện đúng thứ hệ thống đang áp dụng.
        var s = await GamificationEngine.ReadSettingsAsync(db, request.OfferingId, ct);

        return Result<GamificationSettingsDto>.Ok(GamificationSettingsDto.From(s));
    }
}

// ── Lưu cấu hình ────────────────────────────────────────────────────────────────────────

public sealed record SaveGamificationSettingsCommand(
    bool IsEnabled, LeaderboardVisibility LeaderboardVisibility, int LeaderboardSize)
    : ICommand<Result<GamificationSettingsDto>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class SaveGamificationSettingsValidator : AbstractValidator<SaveGamificationSettingsCommand>
{
    public SaveGamificationSettingsValidator() =>
        RuleFor(x => x.LeaderboardSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số hạng hiển thị phải nằm trong khoảng 1–100.");
}

public sealed class SaveGamificationSettingsHandler(IAppDbContext db)
    : ICommandHandler<SaveGamificationSettingsCommand, Result<GamificationSettingsDto>>
{
    public async Task<Result<GamificationSettingsDto>> Handle(
        SaveGamificationSettingsCommand request, CancellationToken ct)
    {
        var lop = await GamificationAccess.OfferingForInstructorAsync(
            db, request.OfferingId, request.InstructorId, ct);

        if (lop is null) return Result<GamificationSettingsDto>.NotFound("Không tìm thấy lớp học phần.");

        var s = await db.OfferingGamificationSettings
            .SingleOrDefaultAsync(x => x.OfferingId == request.OfferingId, ct);

        if (s is null)
        {
            s = OfferingGamificationSettings.DefaultFor(request.OfferingId);
            db.OfferingGamificationSettings.Add(s);
        }

        // Bật hiện tên thật là một quyết định có hậu quả: bảng xếp hạng công khai tạo động lực
        // cho nhóm dẫn đầu và làm nản lòng nhóm cuối — vốn là nhóm cần động lực nhất. Ở đây chỉ
        // ghi đúng thứ giảng viên chọn; mặc định của hệ thống vẫn là ẩn danh.
        s.IsEnabled = request.IsEnabled;
        s.LeaderboardVisibility = request.LeaderboardVisibility;
        s.LeaderboardSize = request.LeaderboardSize;

        await db.SaveChangesAsync(ct);

        return Result<GamificationSettingsDto>.Ok(GamificationSettingsDto.From(s));
    }
}
