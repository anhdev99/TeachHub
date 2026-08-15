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
    Guid Id, Guid? AssignmentId, Guid? FinalAssessmentId, Guid OfferingId,
    Guid? GroupId, int AttemptNo, bool IsCurrent,
    SubmissionStatus Status, DateTimeOffset? SubmittedAt, bool IsLate,
    decimal? Score, string? Feedback, Dictionary<Guid, decimal> RubricScores,
    List<SubmissionFileDto> Files)
{
    /// <summary>
    /// <paramref name="thayDiem"/> = false thì điểm, nhận xét và điểm từng tiêu chí bị che.
    /// Luật này nằm ở tầng Application chứ không ở controller là chủ ý: cùng một DTO được
    /// ba màn hình dùng lại, và nếu luật nằm ở controller thì màn hình thứ hai gọi thẳng
    /// query sẽ lộ điểm mà không ai nhận ra.
    /// </summary>
    public static SubmissionDto From(Submission s, bool thayDiem = true) => new(
        s.Id, s.AssignmentId, s.FinalAssessmentId, s.OfferingId, s.GroupId,
        s.AttemptNo, s.IsCurrent, s.Status, s.SubmittedAt, s.IsLate,
        thayDiem ? s.Score : null,
        thayDiem ? s.Feedback : null,
        thayDiem ? s.RubricScores : [],
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
    /// Người gọi có được thấy điểm và nhận xét của bài này không.
    ///
    /// Giảng viên: luôn thấy. Sinh viên: CHỈ sau khi giảng viên bấm "Trả bài". Đây không
    /// phải chi tiết nhỏ — giảng viên chấm rải rác nhiều ngày, và nếu điểm hiện ngay khi
    /// vừa chấm thì người được chấm trước sẽ so bì với người chưa được chấm. Hai trạng thái
    /// <c>Graded</c> và <c>Returned</c> tách biệt chính là để giải quyết chuyện đó.
    /// </summary>
    public static async Task<bool> ThayDiemAsync(
        IAppDbContext db, Submission s, Guid userId, CancellationToken ct)
    {
        if (s.Status == SubmissionStatus.Returned) return true;

        return await db.CourseOfferings.AnyAsync(o => o.Id == s.OfferingId &&
            db.Subjects.Any(sub => sub.Id == o.SubjectId && sub.InstructorId == userId), ct);
    }

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
