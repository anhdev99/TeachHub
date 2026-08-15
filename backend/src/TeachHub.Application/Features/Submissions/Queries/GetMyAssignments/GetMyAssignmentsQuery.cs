using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Assignments;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Submissions.Queries.GetMyAssignments;

public sealed record MyAssignmentDto(
    Guid Id, string Title, string? Description, DateTimeOffset? DueAt,
    bool AllowLateSubmission, decimal MaxScore,
    List<SubmissionSlotDto> Slots,
    Guid? SubmissionId, SubmissionStatus? Status, bool IsLate,
    int AttemptCount, decimal? Score);

public sealed record GetMyAssignmentsQuery(Guid OfferingId, Guid StudentUserId)
    : IQuery<Result<List<MyAssignmentDto>>>;

public sealed class GetMyAssignmentsHandler(IAppDbContext db)
    : IQueryHandler<GetMyAssignmentsQuery, Result<List<MyAssignmentDto>>>
{
    public async Task<Result<List<MyAssignmentDto>>> Handle(
        GetMyAssignmentsQuery request, CancellationToken ct)
    {
        var lop = await (from e in db.Enrollments.AsNoTracking()
                         where e.OfferingId == request.OfferingId && e.UserId == request.StudentUserId
                         join o in db.CourseOfferings on e.OfferingId equals o.Id
                         select new { EnrollmentId = e.Id, o.SubjectVersionId })
                        .SingleOrDefaultAsync(ct);

        // Không học lớp này ⇒ 404, không bao giờ 403.
        if (lop is null) return Result<List<MyAssignmentDto>>.NotFound("Không tìm thấy lớp học phần.");

        var baiTap = await db.Assignments.AsNoTracking()
            .Include(a => a.Slots)
            .Where(a => a.SubjectVersionId == lop.SubjectVersionId)
            .OrderBy(a => a.DueAt ?? DateTimeOffset.MaxValue).ThenBy(a => a.Title)
            .ToListAsync(ct);

        var ids = baiTap.Select(a => a.Id).ToList();

        var baiNop = await db.Submissions.AsNoTracking()
            .Where(s => s.EnrollmentId == lop.EnrollmentId
                     && s.AssignmentId != null && ids.Contains(s.AssignmentId.Value))
            .Select(s => new
            {
                AssignmentId = s.AssignmentId!.Value,
                s.Id, s.Status, s.IsLate, s.IsCurrent, s.Score,
            })
            .ToListAsync(ct);

        return Result<List<MyAssignmentDto>>.Ok([.. baiTap.Select(a =>
        {
            var cua = baiNop.Where(s => s.AssignmentId == a.Id).ToList();
            var hienHanh = cua.FirstOrDefault(s => s.IsCurrent);

            return new MyAssignmentDto(
                a.Id, a.Title, a.Description, a.DueAt, a.AllowLateSubmission, a.MaxScore,
                [.. a.Slots.OrderBy(s => s.OrderIndex).Select(SubmissionSlotDto.From)],
                hienHanh?.Id, hienHanh?.Status, hienHanh?.IsLate ?? false,
                cua.Count, hienHanh?.Score);
        })]);
    }
}
