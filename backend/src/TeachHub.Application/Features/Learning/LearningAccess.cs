using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Domain.Learning;
using TeachHub.Domain.Teaching;

namespace TeachHub.Application.Features.Learning;

/// <summary>
/// Once an offering is closed, every WRITE path open to students has to close with it.
///
/// <c>OfferingStatus</c> has had three values since Phase 2, but until this fix the ONLY place that
/// read it was <see cref="CourseOffering.AcceptsJoinCode"/>. Which meant
/// <c>POST /api/offerings/{id}/close</c> flipped a label and froze one gradebook snapshot, and then
/// students went right on submitting work, taking quizzes and marking progress as usual — with the
/// "as of closing time" snapshot being compared against a gradebook that kept on changing behind
/// its back.
///
/// Three decisions come with this, every one of them deliberate:
///
/// 1. <b>409, not 404.</b> Completely unlike the not-yours case: here the student knows perfectly
///    well that this offering is real and that they are enrolled in it. Hiding it behind a 404 only
///    makes them think the system is broken and message the instructor. Ships with a readable
///    Vietnamese <c>detail</c>, because that is the only thing the UI displays.
///
/// 2. <b>Lock the WRITE paths only.</b> Students must still be able to review lessons, grades,
///    submitted work and already-unlocked hints after the offering ends — locking reads wipes out
///    the entire value of having studied.
///
/// 3. <b>Do NOT lock the instructor.</b> Grading normally happens AFTER the offering ends; blocking
///    the instructor from grading means nobody dares close an offering and the feature is useless.
///
/// Centralised for exactly the same reason as <see cref="LearningAccess"/>: every hand-copied
/// duplicate is one more place to forget, and forgetting one place means the lock does not exist.
/// </summary>
public static class OfferingLock
{
    /// <summary>
    /// The message returned to the user. Takes the verb of the operation so the Vietnamese sentence
    /// reads properly — "you can no longer do that" is the kind of message that leaves the reader
    /// guessing what exactly they just did wrong.
    /// </summary>
    public static string ClosedMessage(string action) =>
        $"Lớp học phần đã kết thúc nên không {action} được nữa.";

    /// <summary>Has offering <paramref name="offeringId"/> been closed.</summary>
    public static Task<bool> IsClosedAsync(IAppDbContext db, Guid offeringId, CancellationToken ct) =>
        db.CourseOfferings.AsNoTracking()
            .AnyAsync(o => o.Id == offeringId && o.Status == OfferingStatus.Closed, ct);

    /// <summary>
    /// Has the offering that <paramref name="enrollmentId"/> belongs to been closed. The
    /// most-used overload: most student-side handlers already had to resolve the enrollment in
    /// order to check permissions, so this costs nothing extra.
    /// </summary>
    public static async Task<bool> IsClosedForEnrollmentAsync(
        IAppDbContext db, Guid enrollmentId, CancellationToken ct)
    {
        var status = await (from e in db.Enrollments.AsNoTracking()
                            where e.Id == enrollmentId
                            join o in db.CourseOfferings on e.OfferingId equals o.Id
                            select (OfferingStatus?)o.Status).FirstOrDefaultAsync(ct);

        // The nullable cast is deliberate: `FirstOrDefaultAsync` over a bare enum returns `Draft`
        // when there is no row, i.e. "not closed" — an answer that is both wrong and silent if the
        // enrollment has disappeared.
        return status == OfferingStatus.Closed;
    }
}

/// <summary>
/// A student may only touch content from the version that THEIR OWN OFFERING has pinned. The check
/// is gathered in one place: every hand-copied duplicate is one more place to forget, and forgetting
/// one place means a student in this class marking a lesson of another class as complete.
/// </summary>
public static class LearningAccess
{
    /// <summary>The enrollment of <paramref name="userId"/> for that lesson, or null if they have no access.</summary>
    public static Task<Guid?> EnrollmentForLessonAsync(
        IAppDbContext db, Guid lessonId, Guid userId, CancellationToken ct) =>
        (from l in db.Lessons.AsNoTracking()
         where l.Id == lessonId
         join c in db.Chapters on l.ChapterId equals c.Id
         join o in db.CourseOfferings on c.SubjectVersionId equals o.SubjectVersionId
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select (Guid?)e.Id).FirstOrDefaultAsync(ct);

    public static Task<Guid?> EnrollmentForComponentAsync(
        IAppDbContext db, Guid componentId, Guid userId, CancellationToken ct) =>
        (from cp in db.LessonComponents.AsNoTracking()
         where cp.Id == componentId
         join l in db.Lessons on cp.LessonId equals l.Id
         join c in db.Chapters on l.ChapterId equals c.Id
         join o in db.CourseOfferings on c.SubjectVersionId equals o.SubjectVersionId
         join e in db.Enrollments on o.Id equals e.OfferingId
         where e.UserId == userId
         select (Guid?)e.Id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Has the lesson containing <paramref name="componentId"/> been unlocked for this learner?
    /// </summary>
    /// <remarks>
    /// Being enrolled does NOT mean being allowed to touch every lesson. `UnlockEngine` opens them
    /// up gradually as progress is made, and if that check only lives in the query that builds the
    /// study page then it is nothing but a padlock PAINTED ON THE UI: every other endpoint that
    /// takes a lesson id still serves it as usual.
    ///
    /// This rebuilds the chapter tree and then calls the very same `UnlockEngine.Evaluate` that the
    /// study page uses — deliberately not a "leaner" check written just for this spot. Two parallel
    /// ways of computing unlock state are a guarantee that one day they will answer differently,
    /// and the looser of the two is the one that decides.
    /// </remarks>
    public static async Task<bool> IsComponentUnlockedAsync(
        IAppDbContext db, Guid componentId, Guid enrollmentId, CancellationToken ct)
    {
        var location = await db.LessonComponents.AsNoTracking()
            .Where(cp => cp.Id == componentId)
            .Join(db.Lessons, cp => cp.LessonId, l => l.Id, (cp, l) => new { l.Id, l.ChapterId })
            .SingleOrDefaultAsync(ct);

        if (location is null) return false;

        var chapter = await db.Chapters.AsNoTracking()
            .Where(c => c.Id == location.ChapterId)
            .Select(c => new { c.Id, c.UnlockRule })
            .SingleAsync(ct);

        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => l.ChapterId == location.ChapterId)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new { l.Id, l.Title, l.IsRequiredToUnlockNext })
            .ToListAsync(ct);

        var components = await db.LessonComponents.AsNoTracking()
            .Where(cp => lessons.Select(l => l.Id).Contains(cp.LessonId))
            .Select(cp => new { cp.Id, cp.LessonId, cp.IsRequired })
            .ToListAsync(ct);

        var completedLessonIds = await db.LessonProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonId).ToListAsync(ct);

        var completedComponentIds = await db.ComponentProgresses.AsNoTracking()
            .Where(p => p.EnrollmentId == enrollmentId && p.Status == ProgressStatus.Completed)
            .Select(p => p.LessonComponentId).ToListAsync(ct);

        var node = new ChapterNode(chapter.Id, chapter.UnlockRule, [.. lessons.Select(l =>
            new LessonNode(l.Id, l.Title, l.IsRequiredToUnlockNext,
                [.. components.Where(cp => cp.LessonId == l.Id)
                              .Select(cp => new ComponentNode(cp.Id, cp.IsRequired))]))]);

        var access = UnlockEngine.Evaluate(node, Progress.With(completedLessonIds, completedComponentIds));
        return access.GetValueOrDefault(location.Id, LessonAccess.Unlocked) != LessonAccess.Locked;
    }
}
