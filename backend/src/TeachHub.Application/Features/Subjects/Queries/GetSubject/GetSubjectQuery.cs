using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Subjects.Queries.GetSubject;

public sealed record GetSubjectQuery(Guid SubjectId, Guid InstructorId) : IQuery<Result<SubjectDto>>;

public sealed class GetSubjectQueryHandler(IAppDbContext db)
    : IQueryHandler<GetSubjectQuery, Result<SubjectDto>>
{
    public async Task<Result<SubjectDto>> Handle(GetSubjectQuery request, CancellationToken ct)
    {
        var s = await db.Subjects
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.SubjectId && x.InstructorId == request.InstructorId, ct);

        // Không phải của mình ⇒ 404, không bao giờ 403: 403 xác nhận id có tồn tại.
        return s is null
            ? Result<SubjectDto>.NotFound("Không tìm thấy môn học.")
            : Result<SubjectDto>.Ok(SubjectDto.From(s));
    }
}
