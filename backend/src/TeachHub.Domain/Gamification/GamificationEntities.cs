using TeachHub.Domain.Common;

namespace TeachHub.Domain.Gamification;

/// <summary>
/// ONE row of the XP rule table of ONE course offering. The numbers live here and not in code:
/// hard-coding "finishing a lesson = 10 XP" means every time an instructor wants to change the
/// incentives of a class it takes a redeploy, so in practice nobody ever changes it and the
/// whole mechanism withers away.
/// </summary>
public class XpRule : Entity, IAuditable
{
    public required Guid OfferingId { get; set; }
    public required XpEventType EventType { get; set; }
    public required int Points { get; set; }

    /// <summary>"At least" threshold on the value carried by the event. null = not considered.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Maximum number of awards within ONE local day. null = no cap.</summary>
    public int? DailyCap { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public XpRuleSpec ToSpec() => new()
    {
        EventType = EventType, Points = Points, MinValue = MinValue,
        DailyCap = DailyCap, IsEnabled = IsEnabled,
    };
}

/// <summary>
/// The XP ledger: every row is ONE award that actually happened. A learner's total XP is always
/// the sum of this ledger, never a single overwritten number — with a ledger you can answer
/// "why do I have 240 XP", without one you cannot.
///
/// This is a student's learning record: NEVER delete it, not even a soft delete.
/// </summary>
public class XpEvent : Entity
{
    /// <summary>
    /// Gap #2: points at the enrollment, not at the user. The same person retaking this course
    /// next term is a different enrollment — point it at the user and the XP of two terms bleeds
    /// together and the leaderboard is wrong.
    /// </summary>
    public required Guid EnrollmentId { get; set; }

    public required XpEventType Type { get; set; }
    public required int Points { get; set; }

    /// <summary>
    /// The de-duplication key, e.g. <c>"lesson:{lessonId}"</c>, <c>"quiz_attempt:{attemptId}"</c>,
    /// <c>"streak:7"</c>. Guarded by a unique index <c>(enrollment_id, dedupe_key)</c> in the DB.
    ///
    /// Without it, every re-sync is another award, and the "mark complete then unmark it" trick
    /// becomes an infinite XP printer — the number one hole in every gamification system.
    /// </summary>
    public required string DedupeKey { get; set; }

    /// <summary>
    /// When the ACTIVITY actually happened (submitting work, finishing a lesson), NOT when it was
    /// recorded. Streaks read exactly this column: take the recording time instead and a student
    /// who studied every day of the week but opened the page only once ends up with streak = 1.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; set; }

    /// <summary>Id of the source (lesson, submission, quiz attempt) — for tracing only, deliberately not declared as an FK.</summary>
    public Guid? SourceId { get; set; }
}

/// <summary>
/// A badge belongs to the INSTRUCTOR, not to a single class — same shape as the question bank:
/// author it once, use it for every class and every term.
/// </summary>
public class Badge : Entity, IAuditable
{
    public required Guid InstructorId { get; set; }

    /// <summary>A stable slug code (<c>[a-z0-9_]+</c>) — the UI picks the icon from this code.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>A single emoji as the icon. Real images are a later Phase's job; an emoji is enough and cheap.</summary>
    public string? Icon { get; set; }

    public BadgeLogic Logic { get; set; } = BadgeLogic.All;

    /// <summary>
    /// The conditions declared as DATA, stored as jsonb. Empty = not finished being declared ⇒
    /// never awarded (<see cref="BadgeEvaluator.IsEarned"/> fails closed).
    /// </summary>
    public List<BadgeCondition> Conditions { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public BadgeSpec ToSpec() => new(Id, Code, Logic, Conditions);
}

/// <summary>One award of a badge. Once awarded it is never taken back — has a unique (enrollment, badge).</summary>
public class EarnedBadge : Entity
{
    public required Guid BadgeId { get; set; }
    public required Guid EnrollmentId { get; set; }
    public required DateTimeOffset EarnedAt { get; set; }
}

/// <summary>
/// ANONYMOUS BY DEFAULT, and that is a business decision rather than a default chosen for
/// convenience: a leaderboard with real names motivates the group at the top but discourages the
/// group at the bottom — precisely the group that needs the motivation most. Changing the default
/// value here is changing policy.
/// </summary>
public enum LeaderboardVisibility { Anonymous, RealNames }

/// <summary>
/// The learning-motivation settings of ONE class. A separate entity instead of extra columns on
/// <c>CourseOffering</c>: the course offering is the hottest table in the system, and every column
/// added to it is a column every other query has to drag along.
/// </summary>
public class OfferingGamificationSettings : Entity, IAuditable
{
    public required Guid OfferingId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public LeaderboardVisibility LeaderboardVisibility { get; set; } = LeaderboardVisibility.Anonymous;

    /// <summary>How many ranks the leaderboard shows. Not the whole class — a short top list motivates more.</summary>
    public int LeaderboardSize { get; set; } = 10;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Default settings for a class that has never been configured — anonymous, enabled.</summary>
    public static OfferingGamificationSettings DefaultFor(Guid offeringId) =>
        new() { OfferingId = offeringId };
}
