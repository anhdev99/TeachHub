using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Commands.SubmitAssignment;

public sealed record SubmitAssignmentCommand(Guid AssignmentId, Guid StudentUserId)
    : ICommand<Result<SubmissionDto>>;

public sealed class SubmitAssignmentHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SubmitAssignmentCommand, Result<SubmissionDto>>
{
    public async Task<Result<SubmissionDto>> Handle(SubmitAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await db.Assignments
            .Include(a => a.Slots)
            .SingleOrDefaultAsync(a => a.Id == request.AssignmentId, ct);

        if (assignment is null) return Result<SubmissionDto>.NotFound("Không tìm thấy bài tập.");

        var ghiDanh = await SubmissionAccess.EnrollmentForAssignmentAsync(
            db, request.AssignmentId, request.StudentUserId, ct);

        if (ghiDanh is null) return Result<SubmissionDto>.NotFound("Không tìm thấy bài tập.");

        var nhap = await db.Submissions
            .Include(s => s.Files)
            .SingleOrDefaultAsync(s => s.AssignmentId == request.AssignmentId
                                    && s.EnrollmentId == ghiDanh.EnrollmentId
                                    && s.IsCurrent
                                    && s.Status == SubmissionStatus.Draft, ct);

        if (nhap is null) return Result<SubmissionDto>.Invalid("Chưa có tệp nào được tải lên.");

        var now = clock.GetUtcNow();

        if (assignment.DueAt is not null && now > assignment.DueAt && !assignment.AllowLateSubmission)
            return Result<SubmissionDto>.Forbidden(
                $"Bài tập đã hết hạn lúc {assignment.DueAt:dd/MM/yyyy HH:mm} và không nhận nộp trễ.");

        var daCo = nhap.Files
            .GroupBy(f => f.SlotKey)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        if (!assignment.CanBeSubmittedWith(daCo))
        {
            // Nêu đích danh phần còn thiếu. Chỉ nói "chưa đủ điều kiện" là sinh viên bấm
            // lại năm lần rồi nhắn tin hỏi.
            var thieu = assignment.MissingRequiredSlots(daCo).Select(s => s.Label);
            return Result<SubmissionDto>.Invalid($"Chưa nộp các phần bắt buộc: {string.Join(", ", thieu)}.");
        }

        // Nộp lại: mọi bản trước chuyển sang lịch sử, KHÔNG xóa (Gap #5). Không tự gọi
        // BeginTransactionAsync — TransactionBehavior đã bọc sẵn mọi ICommand (ADR-010),
        // nên hạ cờ is_current và đóng dấu bản mới hoặc cùng vào, hoặc cùng không.
        var truoc = await db.Submissions
            .Where(s => s.AssignmentId == request.AssignmentId
                     && s.EnrollmentId == ghiDanh.EnrollmentId
                     && s.IsCurrent && s.Id != nhap.Id)
            .ToListAsync(ct);

        foreach (var t in truoc) t.IsCurrent = false;

        nhap.MarkSubmitted(now, assignment.DueAt);

        await db.SaveChangesAsync(ct);

        return Result<SubmissionDto>.Ok(SubmissionDto.From(nhap));
    }
}
