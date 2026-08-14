using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Subjects.Commands.PublishVersion;

/// <summary>
/// Ghi dữ liệu ⇒ khai là ICommand chứ không phải IQuery. Đây không phải hình thức:
/// TransactionBehavior chỉ mở transaction cho request cài IBaseCommand, nên khai sai
/// kiểu là mất luôn tính nguyên tử của bước đổi trạng thái bên dưới.
/// </summary>
public sealed record PublishVersionCommand(Guid VersionId, Guid InstructorId) : ICommand<Result>;

public sealed class PublishVersionCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<PublishVersionCommand, Result>
{
    public async Task<Result> Handle(PublishVersionCommand request, CancellationToken ct)
    {
        var version = await db.SubjectVersions.SingleOrDefaultAsync(v => v.Id == request.VersionId, ct);
        if (version is null) return Result.NotFound("Không tìm thấy phiên bản.");

        var subject = await db.Subjects.SingleOrDefaultAsync(
            s => s.Id == version.SubjectId && s.InstructorId == request.InstructorId, ct);

        // Không phải chủ sở hữu ⇒ trả 404 chứ không 403: 403 vô tình xác nhận
        // "id này có tồn tại", đủ để dò ra cấu trúc dữ liệu của giảng viên khác.
        if (subject is null) return Result.NotFound("Không tìm thấy phiên bản.");

        if (version.Status != SubjectVersionStatus.Draft)
            return Result.Conflict(
                $"Phiên bản đang ở trạng thái {version.Status}, chỉ xuất bản được bản nháp.");

        var now = clock.GetUtcNow();

        // Toàn bộ chuyển trạng thái nằm trong một SaveChanges, và TransactionBehavior đã bọc
        // sẵn cả handler trong một transaction. Tách ra nhiều lần lưu thì một lỗi giữa chừng
        // sẽ để lại hai bản published cùng lúc.
        var dangPublished = await db.SubjectVersions
            .Where(v => v.SubjectId == version.SubjectId && v.Status == SubjectVersionStatus.Published)
            .ToListAsync(ct);

        foreach (var cu in dangPublished) cu.Archive(now);

        version.Publish(now);
        subject.CurrentVersionId = version.Id;

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
