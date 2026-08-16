using TeachHub.Domain.Content;

namespace TeachHub.Domain.Learning;

public enum LessonAccess { Locked, Unlocked }

public sealed record ComponentNode(Guid Id, bool IsRequired);
public sealed record LessonNode(Guid Id, string Title, bool IsRequiredToUnlockNext, List<ComponentNode> Components);
public sealed record ChapterNode(Guid Id, UnlockRule Rule, List<LessonNode> Lessons);

public sealed record Progress(HashSet<Guid> CompletedLessons, HashSet<Guid> CompletedComponents)
{
    public static Progress Empty => new([], []);

    public static Progress With(IEnumerable<Guid>? completedLessons = null,
                                IEnumerable<Guid>? completedComponents = null) =>
        new([.. completedLessons ?? []], [.. completedComponents ?? []]);
}

/// <summary>
/// Pure function, no I/O. Kept away from the database so that every unlock combination is testable
/// in milliseconds — this is the kind of logic where getting one edge case wrong leaves a student
/// stuck, unable to carry on with the course, and you get the message about it at 11pm.
/// </summary>
public static class UnlockEngine
{
    public static Dictionary<Guid, LessonAccess> Evaluate(ChapterNode chapter, Progress progress)
    {
        var result = new Dictionary<Guid, LessonAccess>();

        if (chapter.Rule == UnlockRule.Free)
        {
            foreach (var l in chapter.Lessons) result[l.Id] = LessonAccess.Unlocked;
            return result;
        }

        var unlocked = true;
        foreach (var lesson in chapter.Lessons)
        {
            result[lesson.Id] = unlocked ? LessonAccess.Unlocked : LessonAccess.Locked;

            // Only a lesson explicitly marked as required is allowed to block the next one.
            if (unlocked && lesson.IsRequiredToUnlockNext && !IsComplete(lesson, progress))
                unlocked = false;
        }

        return result;
    }

    /// <summary>
    /// Which lesson is blocking <paramref name="lessonId"/>, or null if that lesson is unlocked.
    /// The UI uses this to write "Complete 'Lesson 2 — Hooks' to unlock this one" instead of just
    /// showing a mute padlock.
    /// </summary>
    public static LessonNode? BlockingLesson(ChapterNode chapter, Guid lessonId, Progress progress)
    {
        if (chapter.Rule == UnlockRule.Free) return null;

        foreach (var lesson in chapter.Lessons)
        {
            if (lesson.Id == lessonId) return null;   // reached it without being blocked ⇒ unlocked

            if (lesson.IsRequiredToUnlockNext && !IsComplete(lesson, progress))
                return lesson;
        }

        return null;
    }

    public static bool IsComplete(LessonNode lesson, Progress progress) =>
        progress.CompletedLessons.Contains(lesson.Id)
        && lesson.Components
            .Where(c => c.IsRequired)
            .All(c => progress.CompletedComponents.Contains(c.Id));

    public static decimal CompletionPercent(ChapterNode chapter, Progress progress)
    {
        if (chapter.Lessons.Count == 0) return 0m;   // empty chapter: 0%, no division by zero

        var completed = chapter.Lessons.Count(l => IsComplete(l, progress));
        return Math.Round(completed * 100m / chapter.Lessons.Count, 1);
    }
}
