using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Assignments.Commands.ManageSlots;

/// <summary>
/// Đổi thứ tự một slot nộp bài. Tách thành lệnh riêng thay vì thêm <c>order_index</c> vào
/// <see cref="UpdateSubmissionSlotCommand"/> — cùng khuôn với <c>ReorderComponentCommand</c>
/// và <c>ReorderLessonCommand</c> đã có từ Phase 1.
///
/// Lý do tách: lệnh sửa slot gửi nguyên cả cấu hình (nhãn, định dạng, dung lượng). Nhét
/// thêm thứ tự vào đó nghĩa là mỗi cú kéo-thả phải gửi kèm toàn bộ cấu hình, và hai người
/// sửa cùng lúc sẽ ghi đè cấu hình của nhau chỉ vì một người kéo lại thứ tự.
/// </summary>
public sealed record ReorderSubmissionSlotCommand(int ToIndex) : ICommand<Result>
{
    public Guid SlotId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class ReorderSubmissionSlotValidator : AbstractValidator<ReorderSubmissionSlotCommand>
{
    public ReorderSubmissionSlotValidator() =>
        RuleFor(x => x.ToIndex).GreaterThanOrEqualTo(0).WithMessage("Vị trí đích không được âm.");
}

public sealed class ReorderSubmissionSlotHandler(IAppDbContext db)
    : ICommandHandler<ReorderSubmissionSlotCommand, Result>
{
    public async Task<Result> Handle(ReorderSubmissionSlotCommand request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsSlotAsync(db, request.SlotId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy slot nộp bài.");

        var slot = await db.SubmissionSlots.SingleAsync(s => s.Id == request.SlotId, ct);

        // Nạp anh em cùng chủ sở hữu. Slot thuộc bài tập HOẶC đợt cuối môn (ADR-003), nên
        // phải rẽ theo đúng nhánh — gom cả hai lại là đánh số lẫn slot của hai chủ khác nhau.
        var anhEm = await db.SubmissionSlots
            .Where(s => slot.AssignmentId != null
                ? s.AssignmentId == slot.AssignmentId
                : s.FinalAssessmentId == slot.FinalAssessmentId)
            .ToListAsync(ct);

        // Sắp theo OrderIndex TRƯỚC khi đánh số lại: EF nạp collection không kèm ORDER BY, nên
        // thứ tự phần tử trả về là thứ tự heap của Postgres. Đánh số theo thứ tự nạp là hoán
        // vị các phần một cách ngẫu nhiên — không exception, không test nào đỏ chắc chắn, chỉ
        // thỉnh thoảng thứ tự đổi sau khi xóa một slot. Cùng lỗi đã gặp ở Chapter/Lesson.
        anhEm.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));

        anhEm.Remove(slot);
        anhEm.Insert(Math.Clamp(request.ToIndex, 0, anhEm.Count), slot);

        // OrderIndex phải luôn liên tục 0..n-1. Để hở thì kéo-thả dần tạo ra khoảng trống và
        // thứ tự hiển thị thành không xác định.
        for (var i = 0; i < anhEm.Count; i++) anhEm[i].OrderIndex = i;

        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
