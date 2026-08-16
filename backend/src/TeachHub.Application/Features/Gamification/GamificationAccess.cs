using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Gamification;

/// <param name="TimeZoneId">The offering's time zone — every "day" calculation of the streak goes through it.</param>
public sealed record OfferingContext(Guid OfferingId, Guid InstructorId, string TimeZoneId);

public sealed record LearnerContext(
    Guid EnrollmentId, Guid OfferingId, Guid InstructorId, string TimeZoneId);

/// <summary>
/// Concentrates every ownership check of the gamification feature set in ONE place. Each
/// hand-copied variant is one more place where <c>instructor_id</c> can be forgotten, and
/// forgetting it in a single spot here leaks another instructor's leaderboard and learning
/// progress.
///
/// The ownership condition sits RIGHT INSIDE the query: not found ⇒ 404, never 403.
/// Returning 403 confirms that the id exists, which is a data-probing channel.
/// </summary>
public static class GamificationAccess
{
    public static Task<OfferingContext?> OfferingForInstructorAsync(
        IAppDbContext db, Guid offeringId, Guid instructorId, CancellationToken ct) =>
        (from o in db.CourseOfferings.AsNoTracking()
         where o.Id == offeringId
         join s in db.Subjects on o.SubjectId equals s.Id
         where s.InstructorId == instructorId
         select new OfferingContext(o.Id, s.InstructorId, o.TimeZoneId)).SingleOrDefaultAsync(ct);

    /// <summary>The enrollment of <paramref name="userId"/> in that offering, or null if they are not taking it.</summary>
    public static Task<LearnerContext?> LearnerAsync(
        IAppDbContext db, Guid offeringId, Guid userId, CancellationToken ct) =>
        (from e in db.Enrollments.AsNoTracking()
         where e.OfferingId == offeringId && e.UserId == userId
         join o in db.CourseOfferings on e.OfferingId equals o.Id
         join s in db.Subjects on o.SubjectId equals s.Id
         select new LearnerContext(e.Id, o.Id, s.InstructorId, o.TimeZoneId)).SingleOrDefaultAsync(ct);

    /// <summary>Every still-effective enrollment of the offering. Dropped students do not appear on the leaderboard.</summary>
    public static IQueryable<Enrollment> ActiveEnrollments(IAppDbContext db, Guid offeringId) =>
        db.Enrollments.Where(e => e.OfferingId == offeringId && e.Status != EnrollmentStatus.Dropped);
}
