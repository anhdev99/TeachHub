using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions;

public sealed record SubmissionFileDto(
    Guid Id, Guid SlotId, string SlotKey, string FileName, string? ExternalUrl,
    long SizeBytes, FileProcessingStatus ProcessingStatus, string? ProcessingError,
    bool? StructureCheckPassed, List<string> MissingStructureItems,
    DateTimeOffset UploadedAt)
{
    public static SubmissionFileDto From(SubmissionFile f) => new(
        f.Id, f.SlotId, f.SlotKey, f.FileName, f.ExternalUrl, f.SizeBytes,
        f.ProcessingStatus, f.ProcessingError,
        f.StructureCheckPassed, f.MissingStructureItems, f.UploadedAt);
}

public sealed record SubmissionDto(
    Guid Id, Guid? AssignmentId, Guid OfferingId, int AttemptNo, bool IsCurrent,
    SubmissionStatus Status, DateTimeOffset? SubmittedAt, bool IsLate,
    decimal? Score, string? Feedback,
    List<SubmissionFileDto> Files)
{
    public static SubmissionDto From(Submission s) => new(
        s.Id, s.AssignmentId, s.OfferingId, s.AttemptNo, s.IsCurrent,
        s.Status, s.SubmittedAt, s.IsLate, s.Score, s.Feedback,
        [.. s.Files.OrderBy(f => f.UploadedAt).Select(SubmissionFileDto.From)]);
}

/// <summary>
/// Quyền chạm vào một bài nộp. Sinh viên chỉ thấy bài của CHÍNH MÌNH; giảng viên thấy bài
/// của lớp mình dạy. Cả hai ca không thoả đều trả 404 chứ không 403 — 403 xác nhận id có
/// tồn tại, và một sinh viên dò id tuần tự sẽ đếm được bài nộp của cả lớp.
/// </summary>
public static class SubmissionAccess
{
    public static Task<bool> CanReadAsync(
        IAppDbContext db, Guid submissionId, Guid userId, CancellationToken ct) =>
        db.Submissions.AnyAsync(s => s.Id == submissionId &&
            (db.Enrollments.Any(e => e.Id == s.EnrollmentId && e.UserId == userId) ||
             db.CourseOfferings.Any(o => o.Id == s.OfferingId &&
                 db.Subjects.Any(sub => sub.Id == o.SubjectId && sub.InstructorId == userId))), ct);

    /// <summary>
    /// Enrollment của sinh viên trong lớp đang chạy bài tập này, hoặc null nếu không thuộc lớp.
    /// Suy ra từ token, KHÔNG BAO GIỜ nhận từ client: nhận từ client thì đổi một GUID trong
    /// body là nộp đè lên bài của bạn cùng lớp.
    /// </summary>
    public static Task<EnrollmentRef?> EnrollmentForAssignmentAsync(
        IAppDbContext db, Guid assignmentId, Guid userId, CancellationToken ct) =>
        (from a in db.Assignments.AsNoTracking()
         where a.Id == assignmentId
         join o in db.CourseOfferings on a.SubjectVersionId equals o.SubjectVersionId
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select new EnrollmentRef(e.Id, o.Id)).FirstOrDefaultAsync(ct);
}

public sealed record EnrollmentRef(Guid EnrollmentId, Guid OfferingId);
