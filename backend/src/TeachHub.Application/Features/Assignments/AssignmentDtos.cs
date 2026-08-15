using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Assignments;
using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Assignments;

public sealed record SubmissionSlotDto(
    Guid Id, int OrderIndex, string SlotKey, string Label,
    List<string> AcceptedFileTypes, bool AllowLinkInsteadOfFile,
    int MaxFiles, int MaxFileSizeMb, bool IsRequired,
    List<string> RequiredFiles, List<string> RequiredFolders)
{
    public static SubmissionSlotDto From(SubmissionSlot s) => new(
        s.Id, s.OrderIndex, s.SlotKey, s.Label,
        s.AcceptedFileTypes, s.AllowLinkInsteadOfFile,
        s.MaxFiles, s.MaxFileSizeMb, s.IsRequired,
        s.RequiredStructure?.RequiredFiles ?? [],
        s.RequiredStructure?.RequiredFolders ?? []);
}

public sealed record AssignmentHintDto(Guid Id, int OrderIndex, string Content, decimal PenaltyPercent);

public sealed record AssignmentDto(
    Guid Id, Guid SubjectVersionId, string Title, string? Description,
    SubmissionMode SubmissionMode, DateTimeOffset? DueAt,
    bool AllowLateSubmission, decimal LatePenaltyPercentPerDay,
    Guid? RubricId, decimal MaxScore,
    List<SubmissionSlotDto> Slots, List<AssignmentHintDto> Hints)
{
    public static AssignmentDto From(Assignment a) => new(
        a.Id, a.SubjectVersionId, a.Title, a.Description,
        a.SubmissionMode, a.DueAt, a.AllowLateSubmission, a.LatePenaltyPercentPerDay,
        a.RubricId, a.MaxScore,
        [.. a.Slots.OrderBy(s => s.OrderIndex).Select(SubmissionSlotDto.From)],
        [.. a.Hints.OrderBy(h => h.OrderIndex)
             .Select(h => new AssignmentHintDto(h.Id, h.OrderIndex, h.Content, h.PenaltyPercent))]);
}

/// <summary>
/// Lần ngược từ bài tập / slot về giảng viên sở hữu môn học. Gom về một chỗ vì mọi handler
/// của Phase 3 đều cần đúng phép kiểm này: slotId đến thẳng từ URL, nên thiếu bước join
/// ngược thì bất kỳ giảng viên nào đoán được id là sửa được cấu hình nộp bài của lớp người khác.
///
/// Luôn viết dạng "có tồn tại bản ghi thoả CẢ id LẪN chủ sở hữu" để điều kiện nằm trong
/// câu truy vấn, không nạp rồi lọc.
/// </summary>
public static class AssignmentOwnership
{
    public static Task<bool> OwnsAssignmentAsync(
        IAppDbContext db, Guid assignmentId, Guid instructorId, CancellationToken ct) =>
        db.Assignments.AnyAsync(a => a.Id == assignmentId &&
            db.SubjectVersions.Any(v => v.Id == a.SubjectVersionId &&
                db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId)), ct);

    public static Task<bool> OwnsSlotAsync(
        IAppDbContext db, Guid slotId, Guid instructorId, CancellationToken ct) =>
        db.SubmissionSlots.AnyAsync(sl => sl.Id == slotId &&
            db.Assignments.Any(a => a.Id == sl.AssignmentId &&
                db.SubjectVersions.Any(v => v.Id == a.SubjectVersionId &&
                    db.Subjects.Any(s => s.Id == v.SubjectId && s.InstructorId == instructorId))), ct);

    /// <summary>Bài tập nằm trong bản nháp hay bản đã xuất bản — dùng để chặn sửa bản đã phát cho sinh viên.</summary>
    public static Task<SubjectVersionStatus?> VersionStatusOfAssignmentAsync(
        IAppDbContext db, Guid assignmentId, CancellationToken ct) =>
        (from a in db.Assignments.AsNoTracking()
         where a.Id == assignmentId
         join v in db.SubjectVersions on a.SubjectVersionId equals v.Id
         select (SubjectVersionStatus?)v.Status).FirstOrDefaultAsync(ct);
}
