using Shouldly;
using TeachHub.Domain.Content;
using TeachHub.Domain.Learning;

namespace TeachHub.Domain.Tests;

public class UnlockEngineTests
{
    private static LessonNode Lesson(string title, bool required, params ComponentNode[] components) =>
        new(Guid.CreateVersion7(), title, required, [.. components]);

    private static ComponentNode Component(bool required) => new(Guid.CreateVersion7(), required);

    [Fact]
    public void A_free_chapter_unlocks_every_lesson_from_the_start()
    {
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free,
            [Lesson("Bài 1", required: true), Lesson("Bài 2", required: true)]);

        var result = UnlockEngine.Evaluate(chapter, Progress.Empty);

        result.Values.ShouldAllBe(v => v == LessonAccess.Unlocked);
    }

    [Fact]
    public void A_sequential_chapter_opens_only_the_first_lesson_when_nothing_is_studied_yet()
    {
        var l1 = Lesson("Bài 1", required: true);
        var l2 = Lesson("Bài 2", required: true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [l1, l2]);

        var result = UnlockEngine.Evaluate(chapter, Progress.Empty);

        result[l1.Id].ShouldBe(LessonAccess.Unlocked);
        result[l2.Id].ShouldBe(LessonAccess.Locked);
    }

    [Fact]
    public void Completing_a_required_lesson_unlocks_the_next_one()
    {
        var l1 = Lesson("Bài 1", required: true);
        var l2 = Lesson("Bài 2", required: true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [l1, l2]);

        var result = UnlockEngine.Evaluate(chapter, Progress.With(completedLessons: [l1.Id]));

        result[l2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void A_NON_required_lesson_does_not_block_the_next_one()
    {
        // is_required_to_unlock_next = false => supplementary reading, safe to skip.
        var l1 = Lesson("Bài đọc thêm", required: false);
        var l2 = Lesson("Bài 2", required: true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [l1, l2]);

        var result = UnlockEngine.Evaluate(chapter, Progress.Empty);

        result[l2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void A_lesson_counts_as_complete_only_once_every_required_component_is_done()
    {
        var requiredComponent = Component(required: true);
        var optionalComponent = Component(required: false);
        var l1 = Lesson("Bài 1", required: true, requiredComponent, optionalComponent);
        var l2 = Lesson("Bài 2", required: true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [l1, l2]);

        // Finished reading the content but has not submitted the required exercise yet
        var notDone = Progress.With(completedLessons: [l1.Id], completedComponents: []);
        UnlockEngine.Evaluate(chapter, notDone)[l2.Id].ShouldBe(LessonAccess.Locked);

        var done = Progress.With(completedLessons: [l1.Id], completedComponents: [requiredComponent.Id]);
        UnlockEngine.Evaluate(chapter, done)[l2.Id].ShouldBe(LessonAccess.Unlocked);
    }

    [Fact]
    public void A_locked_lesson_can_name_the_lesson_that_is_blocking_it()
    {
        // Showing only a padlock without saying why is the number one source of "why can't I
        // open this" messages in every LMS.
        var l1 = Lesson("Bài 1 — Hooks", required: true);
        var l2 = Lesson("Bài 2", required: true);
        var l3 = Lesson("Bài 3", required: true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.AfterPreviousCompleted, [l1, l2, l3]);

        UnlockEngine.BlockingLesson(chapter, l3.Id, Progress.Empty)?.Title.ShouldBe("Bài 1 — Hooks");
        UnlockEngine.BlockingLesson(chapter, l1.Id, Progress.Empty).ShouldBeNull();
    }

    [Fact]
    public void Completion_percentage_is_computed_from_the_number_of_finished_lessons()
    {
        var l1 = Lesson("1", true); var l2 = Lesson("2", true); var l3 = Lesson("3", true); var l4 = Lesson("4", true);
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free, [l1, l2, l3, l4]);

        UnlockEngine.CompletionPercent(chapter, Progress.With(completedLessons: [l1.Id, l2.Id]))
            .ShouldBe(50m);
    }

    [Fact]
    public void An_empty_chapter_does_not_cause_a_division_by_zero()
    {
        var chapter = new ChapterNode(Guid.CreateVersion7(), UnlockRule.Free, []);
        UnlockEngine.CompletionPercent(chapter, Progress.Empty).ShouldBe(0m);
    }
}
