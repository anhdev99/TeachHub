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
/// <c>IQuery</c> chứ không phải <c>ICommand</c>: TransactionBehavior nhìn marker
/// <c>IBaseCommand</c> nên nó bỏ qua truy vấn này. Bọc dashboard trong transaction chỉ giữ
/// connection lâu hơn cần thiết mà không đổi được gì.
/// </summary>
public sealed class GetInstructorDashboardHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetInstructorDashboardQuery, InstructorDashboardDto>
{
    public async Task<InstructorDashboardDto> Handle(
        GetInstructorDashboardQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Điều kiện sở hữu nằm TRONG câu truy vấn (ADR-012), không nạp rồi lọc.
        var lopCuaToi = db.CourseOfferings.AsNoTracking()
            .Where(o => db.Subjects.Any(s => s.Id == o.SubjectId && s.InstructorId == request.InstructorId));

        // Mỗi khối dưới đây là MỘT câu truy vấn có projection. Số câu truy vấn là hằng số,
        // không phụ thuộc sĩ số — đây là chỗ dễ sinh N+1 nhất trong cả hệ thống, và với lớp
        // 40 người thì N+1 nghĩa là trang chủ tải hàng giây.
        var lop = await lopCuaToi
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

        var canXuLy = await db.Submissions.AsNoTracking()
            .Where(s => s.IsCurrent && s.Status == SubmissionStatus.Submitted
                     && lopCuaToi.Any(o => o.Id == s.OfferingId))
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

        var sapDenHan = await db.Assignments.AsNoTracking()
            .Where(a => a.DueAt != null && a.DueAt > now && a.DueAt < now.AddDays(7)
                     && lopCuaToi.Any(o => o.SubjectVersionId == a.SubjectVersionId))
            .CountAsync(ct);

        return new InstructorDashboardDto(
            lop.Count, lop.Sum(l => l.ChuaCham), sapDenHan, canXuLy, lop);
    }
}
