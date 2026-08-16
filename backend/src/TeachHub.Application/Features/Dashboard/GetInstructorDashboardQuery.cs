using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Dashboard;

public sealed record CanXuLyDto(
    Guid SubmissionId, Guid OfferingId, string ClassCode, string Title,
    string Who, DateTimeOffset? SubmittedAt, bool IsLate);

public sealed record LopTongQuanDto(
    Guid OfferingId, string SubjectCode, string SubjectName, string ClassCode,
    int StudentCount, int ChuaCham, decimal AverageProgressPercent);

public sealed record InstructorDashboardDto(
    int SoLopDangDay, int SoBaiChuaCham, int SoBaiTapSapDenHan,
    List<CanXuLyDto> CanXuLy, List<LopTongQuanDto> Lop);

public sealed record GetInstructorDashboardQuery(Guid InstructorId)
    : IQuery<InstructorDashboardDto>;

/// <summary>
/// <c>IQuery</c> and not <c>ICommand</c>: TransactionBehavior keys off the <c>IBaseCommand</c>
/// marker, so it skips this query. Wrapping the dashboard in a transaction would only hold a
/// connection longer than necessary without changing anything.
/// </summary>
public sealed class GetInstructorDashboardHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetInstructorDashboardQuery, InstructorDashboardDto>
{
    public async Task<InstructorDashboardDto> Handle(
        GetInstructorDashboardQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // The ownership condition lives INSIDE the query (ADR-012); never load then filter.
        var myOfferings = db.CourseOfferings.AsNoTracking()
            .Where(o => db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.InstructorId));

        // Each block below is ONE query with a projection. The number of queries is constant and
        // does not depend on class size — this is the spot in the whole system most likely to grow
        // an N+1, and with a class of 40 an N+1 here means the home page takes seconds to load.
        var offerings = await myOfferings
            .Select(o => new LopTongQuanDto(
                o.Id,
                db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Code).Single(),
                db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Name).Single(),
                o.ClassCode,
                db.Enrollments.Count(e => e.OfferingId == o.Id),
                db.Submissions.Count(s => s.OfferingId == o.Id && s.IsCurrent
                                       && s.Status == SubmissionStatus.Submitted),
                0m))
            .ToListAsync(ct);

        var needsAttention = await db.Submissions.AsNoTracking()
            .Where(s => s.IsCurrent && s.Status == SubmissionStatus.Submitted
                     && myOfferings.Any(o => o.Id == s.OfferingId))
            .OrderBy(s => s.SubmittedAt)
            .Take(20)
            .Select(s => new CanXuLyDto(
                s.Id, s.OfferingId,
                db.CourseOfferings.Where(o => o.Id == s.OfferingId).Select(o => o.ClassCode).Single(),
                s.AssignmentId != null
                    ? db.Assignments.Where(a => a.Id == s.AssignmentId).Select(a => a.Title).FirstOrDefault()!
                    : db.FinalAssessments.Where(f => f.Id == s.FinalAssessmentId).Select(f => f.Title).FirstOrDefault()!,
                s.GroupId != null
                    ? db.StudentGroups.Where(g => g.Id == s.GroupId).Select(g => g.Name).FirstOrDefault()!
                    : db.Enrollments.Where(e => e.Id == s.EnrollmentId).Select(e => e.FullName).FirstOrDefault()!,
                s.SubmittedAt, s.IsLate))
            .ToListAsync(ct);

        var dueSoon = await db.Assignments.AsNoTracking()
            .Where(a => a.DueAt != null && a.DueAt > now && a.DueAt < now.AddDays(7)
                     && myOfferings.Any(o => o.SubjectVersionId == a.SubjectVersionId))
            .CountAsync(ct);

        return new InstructorDashboardDto(
            offerings.Count, offerings.Sum(o => o.ChuaCham), dueSoon, needsAttention, offerings);
    }
}
