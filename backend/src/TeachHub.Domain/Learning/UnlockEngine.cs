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
/// Hàm thuần, không I/O. Tách khỏi database để test được mọi tổ hợp mở khóa trong mili-giây —
/// đây là loại logic mà sai một ca biên là sinh viên bị kẹt không học tiếp được, và bạn
/// sẽ nhận tin nhắn lúc 11 giờ đêm.
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

        var moKhoa = true;
        foreach (var lesson in chapter.Lessons)
        {
            result[lesson.Id] = moKhoa ? LessonAccess.Unlocked : LessonAccess.Locked;

            // Chỉ bài được đánh dấu bắt buộc mới có quyền chặn bài kế tiếp.
            if (moKhoa && lesson.IsRequiredToUnlockNext && !IsComplete(lesson, progress))
                moKhoa = false;
        }

        return result;
    }

    /// <summary>
    /// Bài nào đang chặn <paramref name="lessonId"/>, hoặc null nếu bài đó đang mở.
    /// Giao diện dùng cái này để ghi "Hoàn thành 'Bài 2 — Hooks' để mở bài này" thay vì
    /// chỉ hiện một ổ khóa câm.
    /// </summary>
    public static LessonNode? BlockingLesson(ChapterNode chapter, Guid lessonId, Progress progress)
    {
        if (chapter.Rule == UnlockRule.Free) return null;

        foreach (var lesson in chapter.Lessons)
        {
            if (lesson.Id == lessonId) return null;   // tới nơi mà chưa bị chặn ⇒ đang mở

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
        if (chapter.Lessons.Count == 0) return 0m;   // chương rỗng: 0%, không chia cho không

        var xong = chapter.Lessons.Count(l => IsComplete(l, progress));
        return Math.Round(xong * 100m / chapter.Lessons.Count, 1);
    }
}
