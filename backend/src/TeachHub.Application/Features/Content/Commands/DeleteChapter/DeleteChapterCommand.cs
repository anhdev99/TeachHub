using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Content.Commands.DeleteChapter;

public sealed record DeleteChapterCommand : ICommand<Result>
{
    public Guid ChapterId { get; init; }
    public Guid InstructorId { get; init; }
}

public sealed class DeleteChapterCommandHandler(IAppDbContext db)
    : ICommandHandler<DeleteChapterCommand, Result>
{
    public async Task<Result> Handle(DeleteChapterCommand request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsChapterAsync(db, request.ChapterId, request.InstructorId, ct))
            return Result.NotFound("Không tìm thấy chương.");

        var chapter = await db.Chapters.SingleAsync(c => c.Id == request.ChapterId, ct);
        var versionId = chapter.SubjectVersionId;

        // Remove → SaveChangesAsync của AppDbContext quy đổi thành xóa MỀM cho ISoftDeletable:
        // nội dung dạy học không bao giờ bị DELETE thật vì bài nộp của sinh viên trỏ vào nó.
        db.Chapters.Remove(chapter);
        await db.SaveChangesAsync(ct);

        // Dồn lại order_index cho liên tục 0..n-1. Bỏ bước này thì kéo-thả lần sau
        // sinh ra thứ tự không xác định.
        var conLai = await db.Chapters
            .Where(c => c.SubjectVersionId == versionId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        for (var i = 0; i < conLai.Count; i++) conLai[i].OrderIndex = i;
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
