using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Content;

namespace TeachHub.Application.Features.Assignments.Queries.GetAssignments;

public sealed record GetAssignmentsQuery(Guid SubjectVersionId, Guid InstructorId)
    : IQuery<Result<List<AssignmentDto>>>;

public sealed class GetAssignmentsHandler(IAppDbContext db)
    : IQueryHandler<GetAssignmentsQuery, Result<List<AssignmentDto>>>
{
    public async Task<Result<List<AssignmentDto>>> Handle(GetAssignmentsQuery request, CancellationToken ct)
    {
        if (!await ContentOwnership.OwnsVersionAsync(db, request.SubjectVersionId, request.InstructorId, ct))
            return Result<List<AssignmentDto>>.NotFound("Không tìm thấy phiên bản môn học.");

        var ds = await db.Assignments.AsNoTracking()
            .Include(a => a.Slots)
            .Include(a => a.Hints)
            .Where(a => a.SubjectVersionId == request.SubjectVersionId)
            .OrderBy(a => a.Title)
            .ToListAsync(ct);

        return Result<List<AssignmentDto>>.Ok([.. ds.Select(AssignmentDto.From)]);
    }
}

public sealed record GetAssignmentQuery(Guid AssignmentId, Guid InstructorId)
    : IQuery<Result<AssignmentDto>>;

public sealed class GetAssignmentHandler(IAppDbContext db)
    : IQueryHandler<GetAssignmentQuery, Result<AssignmentDto>>
{
    public async Task<Result<AssignmentDto>> Handle(GetAssignmentQuery request, CancellationToken ct)
    {
        if (!await AssignmentOwnership.OwnsAssignmentAsync(db, request.AssignmentId, request.InstructorId, ct))
            return Result<AssignmentDto>.NotFound("Không tìm thấy bài tập.");

        var a = await db.Assignments.AsNoTracking()
            .Include(x => x.Slots)
            .Include(x => x.Hints)
            .SingleAsync(x => x.Id == request.AssignmentId, ct);

        return Result<AssignmentDto>.Ok(AssignmentDto.From(a));
    }
}
