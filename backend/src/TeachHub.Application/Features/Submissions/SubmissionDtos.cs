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
    /// When <paramref name="canSeeGrade"/> is false, the score, the feedback and the per-criterion
    /// rubric scores are all blanked out. Keeping this rule in the Application layer instead of in
    /// the controller is deliberate: the very same DTO is reused by three screens, and with the
    /// rule sitting in the controller, the second screen — the one calling the query directly —
    /// would leak the grades and nobody would notice.
    /// </summary>
    public static SubmissionDto From(Submission s, bool canSeeGrade = true) => new(
        s.Id, s.AssignmentId, s.FinalAssessmentId, s.OfferingId, s.GroupId,
        s.AttemptNo, s.IsCurrent, s.Status, s.SubmittedAt, s.IsLate,
        canSeeGrade ? s.Score : null,
        canSeeGrade ? s.Feedback : null,
        canSeeGrade ? s.RubricScores : [],
        [.. s.Files.OrderBy(f => f.UploadedAt).Select(SubmissionFileDto.From)]);
}

/// <summary>
/// Who is allowed to touch a submission. A student only ever sees THEIR OWN work; an instructor
/// sees the work of the offerings they teach. Both failing cases return 404, never 403 — a 403
/// confirms that the id exists, and a student walking ids in sequence could count the submissions
/// of the whole class that way.
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
    /// Whether the caller is allowed to see this submission's score and feedback.
    ///
    /// Instructor: always. Student: ONLY after the instructor has pressed "Trả bài" (return the
    /// work). This is not a minor detail — an instructor grades in bursts spread over several
    /// days, and if the score showed up the instant it was entered, the students graded early
    /// would start comparing themselves with the ones not graded yet. Keeping <c>Graded</c> and
    /// <c>Returned</c> as two separate states is exactly what solves that.
    /// </summary>
    public static async Task<bool> CanSeeGradeAsync(
        IAppDbContext db, Submission s, Guid userId, CancellationToken ct)
    {
        if (s.Status == SubmissionStatus.Returned) return true;

        return await db.CourseOfferings.AnyAsync(o => o.Id == s.OfferingId &&
            db.Subjects.Any(sub => sub.Id == o.SubjectId && sub.InstructorId == userId), ct);
    }

    /// <summary>
    /// The student's enrollment in the offering that runs this assignment, or null when they are
    /// not in that offering. Derived from the token, NEVER accepted from the client: accept it
    /// from the client and changing a single GUID in the body submits on top of a classmate's work.
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
