using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings.Commands.CloseOffering;

/// <param name="Snapshot">
/// The gradebook snapshot produced by THIS command. <c>null</c> when the offering never had a
/// gradebook set up at all — see <paramref name="SnapshotSkippedReason"/>.
/// </param>
/// <param name="SnapshotSkippedReason">
/// Why there is no snapshot. Returning the single word "closed" and then staying silent leaves the
/// instructor to guess whether the grade table was saved — and they will guess that it was.
/// </param>
public sealed record CloseOfferingResultDto(
    Guid OfferingId, OfferingStatus Status,
    GradeSnapshotDto? Snapshot, string? SnapshotSkippedReason);

/// <summary>
/// Closes a course offering: <c>Active → Closed</c> AND takes a gradebook snapshot in the SAME
/// command (Phase 4b plan). <c>OfferingStatus</c> used to carry all three values while no endpoint
/// could actually change it, which left <c>TakeGradeSnapshotCommand</c> with no caller at all.
///
/// No second snapshot path is built here: this re-calls that exact command, so the numbers inside
/// the snapshot always equal the numbers the instructor sees on the gradebook screen.
///
/// Do NOT open a transaction here — <c>TransactionBehavior</c> already wraps every
/// <c>ICommand</c>, and the nested command rides along on that transaction.
/// </summary>
public sealed record CloseOfferingCommand : ICommand<Result<CloseOfferingResultDto>>
{
    public Guid OfferingId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class CloseOfferingCommandHandler(IAppDbContext db, ISender sender)
    : ICommandHandler<CloseOfferingCommand, Result<CloseOfferingResultDto>>
{
    public async Task<Result<CloseOfferingResultDto>> Handle(
        CloseOfferingCommand request, CancellationToken ct)
    {
        // Điều kiện sở hữu NẰM TRONG câu truy vấn. Lớp của giảng viên khác trả 404, không bao
        // giờ 403: 403 xác nhận id đó có thật, và dò id tuần tự là đếm được số lớp của đồng nghiệp.
        var lop = await db.CourseOfferings
            .Where(o => o.Id == request.OfferingId
                        && db.Subjects.Any(s => s.Id == o.SubjectId
                                                && s.InstructorId == request.InstructorId))
            .SingleOrDefaultAsync(ct);

        if (lop is null)
            return Result<CloseOfferingResultDto>.NotFound("Không tìm thấy lớp học phần.");

        // Đóng lần hai KHÔNG chốt thêm một bản nữa. Bản chốt là thứ đem ra đối chiếu khi sinh
        // viên khiếu nại; hai bản trùng nội dung chỉ làm câu hỏi "giữa hai lần đóng lớp có gì
        // đổi không" không trả lời được nữa. Muốn thêm một bản thì đã có nút chốt sổ tường minh.
        if (lop.Status == OfferingStatus.Closed)
            return Result<CloseOfferingResultDto>.Conflict("Lớp học phần này đã đóng.");

        if (lop.Status != OfferingStatus.Active)
            return Result<CloseOfferingResultDto>.Conflict(
                "Chỉ đóng được lớp đang hoạt động; lớp này còn ở trạng thái nháp.");

        // Lớp chưa từng khởi tạo sổ điểm vẫn phải đóng được: chặn lại là dựng ngõ cụt cho giảng
        // viên chỉ dùng phần nội dung — họ không có cách nào kết thúc lớp của mình. Kiểm bằng
        // "có cột điểm nào không" chứ không đọc thông điệp lỗi của TakeGradeSnapshot: phân
        // nhánh theo chuỗi tiếng Việt là thứ hỏng lặng lẽ ngay lần đầu ai đó sửa câu chữ.
        var coSoDiem = await db.GradeColumns.AnyAsync(c => c.OfferingId == request.OfferingId, ct);

        GradeSnapshotDto? ban = null;
        string? lyDoBoQua = null;

        if (coSoDiem)
        {
            var chot = await sender.Send(new TakeGradeSnapshotCommand
            {
                OfferingId = request.OfferingId, InstructorId = request.InstructorId,
            }, ct);

            // Sổ điểm dựng dở (tổng trọng số ≠ 100%) ⇒ KHÔNG đóng lớp. Trả lỗi TRƯỚC khi đụng
            // vào Status: TransactionBehavior chỉ rollback khi có exception, nên một Result lỗi
            // trả về sau khi đã sửa entity vẫn được commit — lớp đóng mà không có bản chốt,
            // đúng thứ mà việc đóng lớp sinh ra để có.
            if (!chot.IsSuccess)
                return chot.Status == ResultStatus.Conflict
                    ? Result<CloseOfferingResultDto>.Conflict(chot.Error!)
                    : Result<CloseOfferingResultDto>.NotFound(chot.Error);

            ban = chot.Value;
        }
        else
        {
            lyDoBoQua = "Lớp chưa có cột điểm nào nên không có bảng điểm để chốt.";
        }

        lop.Status = OfferingStatus.Closed;
        await db.SaveChangesAsync(ct);

        return Result<CloseOfferingResultDto>.Ok(new CloseOfferingResultDto(
            lop.Id, lop.Status, ban, lyDoBoQua));
    }
}
