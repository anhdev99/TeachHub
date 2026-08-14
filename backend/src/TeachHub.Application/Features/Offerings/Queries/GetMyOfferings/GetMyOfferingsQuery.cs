using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;

namespace TeachHub.Application.Features.Offerings.Queries.GetMyOfferings;

public sealed record MyOfferingDto(
    Guid Id, Guid SubjectId, string SubjectCode, string SubjectName,
    string Semester, string ClassCode, Guid EnrollmentId);

public sealed record GetMyOfferingsQuery(Guid UserId) : IQuery<IReadOnlyList<MyOfferingDto>>;

public sealed class GetMyOfferingsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetMyOfferingsQuery, IReadOnlyList<MyOfferingDto>>
{
    public async Task<IReadOnlyList<MyOfferingDto>> Handle(GetMyOfferingsQuery request, CancellationToken ct) =>
        // Lọc theo enrollment.user_id NGAY trong câu truy vấn: sinh viên chỉ thấy lớp mình học.
        //
        // OrderBy đặt TRƯỚC Select: sắp xếp theo property của một record vừa được chiếu ra
        // bằng constructor thì EF không dịch được sang SQL và ném ngay lúc chạy — 500, không
        // phải lỗi biên dịch, nên chỉ lộ khi có người gọi thật.
        await (from e in db.Enrollments.AsNoTracking()
               where e.UserId == request.UserId
               join o in db.CourseOfferings on e.OfferingId equals o.Id
               join s in db.Subjects on o.SubjectId equals s.Id
               orderby s.Code, o.ClassCode
               select new MyOfferingDto(o.Id, s.Id, s.Code, s.Name, o.Semester, o.ClassCode, e.Id))
            .ToListAsync(ct);
}
