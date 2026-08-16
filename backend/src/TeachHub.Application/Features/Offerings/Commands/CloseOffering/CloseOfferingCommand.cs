using MediatR;
using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Offerings.Commands.CloseOffering;

/// <param name="Snapshot">
/// Bản chốt sổ điểm sinh ra bởi CHÍNH lệnh này. <c>null</c> khi lớp chưa từng khởi tạo sổ điểm —
/// xem <paramref name="SnapshotSkippedReason"/>.
/// </param>
/// <param name="SnapshotSkippedReason">
/// Vì sao không có bản chốt. Trả về đúng một chữ "closed" rồi im lặng là để giảng viên tự đoán
/// xem bảng điểm đã được lưu lại hay chưa — và họ sẽ đoán là rồi.
/// </param>
public sealed record CloseOfferingResultDto(
    Guid OfferingId, OfferingStatus Status,
    GradeSnapshotDto? Snapshot, string? SnapshotSkippedReason);

/// <summary>
/// Đóng lớp học phần: <c>Active → Closed</c> VÀ chốt một bản sổ điểm trong CÙNG một lệnh
/// (plan Phase 4b). Trước đây <c>OfferingStatus</c> có đủ ba giá trị nhưng không endpoint nào
/// đổi được nó, nên <c>TakeGradeSnapshotCommand</c> không có ai gọi.
///
/// Không dựng đường chốt sổ thứ hai: gọi lại đúng command đó, để con số trong bản chốt luôn
/// bằng con số giảng viên nhìn thấy trên màn hình sổ điểm.
///
/// KHÔNG tự mở transaction — <c>TransactionBehavior</c> đã bọc mọi <c>ICommand</c>, và command
/// lồng bên trong sẽ đi nhờ transaction đó.
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
