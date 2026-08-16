using Shouldly;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Tests;

public class LessonStructureTests
{
    private static Lesson NewLesson(string title = "x") =>
        new() { Title = title, OrderIndex = 0, ContentType = LessonContentType.Text };

    [Fact]
    public void A_lesson_with_no_components_is_still_valid()
    {
        // Exactly the example in SRS section 5.1: "a chapter has 2 lessons, one lesson has an
        // assignment plus a quiz, the other has nothing".
        var lesson = new Lesson { Title = "Giới thiệu", OrderIndex = 0, ContentType = LessonContentType.Ppt };

        lesson.Components.ShouldBeEmpty();
        lesson.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void A_lesson_can_carry_several_components_of_different_types()
    {
        var lesson = new Lesson { Title = "Hooks", OrderIndex = 0, ContentType = LessonContentType.Ppt };
        var assignmentId = Guid.CreateVersion7();

        lesson.AddComponent(LessonComponentType.Recap, refId: null, new ComponentConfig { RecapQuestionCount = 3 });
        lesson.AddComponent(LessonComponentType.Assignment, refId: assignmentId, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.Reflection, refId: null,
            new ComponentConfig { ReflectionPrompt = "3 điều em nhớ nhất?" });

        lesson.Components.Count.ShouldBe(3);
        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void An_assignment_component_requires_a_ref_id()
    {
        Should.Throw<ArgumentException>(() =>
            NewLesson().AddComponent(LessonComponentType.Assignment, refId: null, new ComponentConfig()));
    }

    [Fact]
    public void A_content_component_must_not_have_a_ref_id()
    {
        Should.Throw<ArgumentException>(() =>
            NewLesson().AddComponent(LessonComponentType.Reflection, refId: Guid.CreateVersion7(), new ComponentConfig()));
    }

    [Fact]
    public void Reordering_components_keeps_order_index_contiguous()
    {
        var lesson = NewLesson();
        lesson.AddComponent(LessonComponentType.Hook, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.Reflection, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.CustomNote, null, new ComponentConfig());

        lesson.MoveComponent(lesson.Components[2].Id, toIndex: 0);

        lesson.Components.Select(c => c.Type)
            .ShouldBe([LessonComponentType.CustomNote, LessonComponentType.Hook, LessonComponentType.Reflection]);
        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Removing_a_component_keeps_order_index_contiguous()
    {
        var lesson = NewLesson();
        lesson.AddComponent(LessonComponentType.Hook, null, new ComponentConfig());
        var middle = lesson.AddComponent(LessonComponentType.Reflection, null, new ComponentConfig());
        lesson.AddComponent(LessonComponentType.CustomNote, null, new ComponentConfig());

        lesson.RemoveComponent(middle.Id);

        lesson.Components.Select(c => c.OrderIndex).ShouldBe([0, 1]);
    }

    [Fact]
    public void Reordering_lessons_in_a_chapter_keeps_order_index_contiguous()
    {
        var chapter = new Chapter { SubjectVersionId = Guid.CreateVersion7(), Title = "Chương 1", OrderIndex = 0 };
        chapter.AddLesson(NewLesson("A"));
        chapter.AddLesson(NewLesson("B"));
        var c = chapter.AddLesson(NewLesson("C"));

        chapter.MoveLesson(c.Id, toIndex: 0);

        chapter.Lessons.Select(l => l.Title).ShouldBe(["C", "A", "B"]);
        chapter.Lessons.Select(l => l.OrderIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Removing_a_lesson_from_a_chapter_keeps_order_index_contiguous()
    {
        var chapter = new Chapter { SubjectVersionId = Guid.CreateVersion7(), Title = "Chương 1", OrderIndex = 0 };
        chapter.AddLesson(NewLesson("A"));
        var middle = chapter.AddLesson(NewLesson("B"));
        chapter.AddLesson(NewLesson("C"));

        chapter.RemoveLesson(middle.Id);

        chapter.Lessons.Select(l => l.Title).ShouldBe(["A", "C"]);
        chapter.Lessons.Select(l => l.OrderIndex).ShouldBe([0, 1]);
    }

    /// <summary>
    /// EF loads collection navigations WITHOUT an ORDER BY, so the element order in the list is
    /// Postgres heap order — after a few deletes and inserts it no longer matches OrderIndex. The
    /// Domain has to sort by OrderIndex itself before renumbering; trusting the load order shuffles
    /// the parts of a lesson at random with no error reported anywhere.
    /// </summary>
    [Fact]
    public void Removing_a_component_when_load_order_disagrees_with_OrderIndex_keeps_the_right_order()
    {
        var lesson = NewLesson();

        // Reproduce exactly what EF hands back: a jumbled list, with OrderIndex as the truth.
        var a = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.Hook, OrderIndex = 0 };
        var b = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.Reflection, OrderIndex = 1 };
        var c = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.CustomNote, OrderIndex = 2 };
        lesson.Components.AddRange([c, a, b]);

        lesson.RemoveComponent(b.Id);

        lesson.Components.OrderBy(x => x.OrderIndex).Select(x => x.Type)
           .ShouldBe([LessonComponentType.Hook, LessonComponentType.CustomNote]);
        a.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
    }

    [Fact]
    public void Dragging_a_component_when_load_order_disagrees_with_OrderIndex_still_lands_correctly()
    {
        var lesson = NewLesson();
        var a = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.Hook, OrderIndex = 0 };
        var b = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.Reflection, OrderIndex = 1 };
        var c = new LessonComponent { LessonId = lesson.Id, Type = LessonComponentType.CustomNote, OrderIndex = 2 };
        lesson.Components.AddRange([c, a, b]);

        // Move the FIRST part (by OrderIndex) to the end. The correct order is [b, c, a].
        lesson.MoveComponent(a.Id, 2);

        b.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
        a.OrderIndex.ShouldBe(2);
    }

    [Fact]
    public void Removing_a_lesson_when_load_order_disagrees_with_OrderIndex_keeps_the_right_order()
    {
        var chapter = new Chapter { Title = "C1", OrderIndex = 0 };
        var a = new Lesson { ChapterId = chapter.Id, Title = "A", OrderIndex = 0, ContentType = LessonContentType.Text };
        var b = new Lesson { ChapterId = chapter.Id, Title = "B", OrderIndex = 1, ContentType = LessonContentType.Text };
        var c = new Lesson { ChapterId = chapter.Id, Title = "C", OrderIndex = 2, ContentType = LessonContentType.Text };
        chapter.Lessons.AddRange([c, a, b]);

        chapter.RemoveLesson(b.Id);

        chapter.Lessons.OrderBy(l => l.OrderIndex).Select(l => l.Title).ShouldBe(["A", "C"]);
        a.OrderIndex.ShouldBe(0);
        c.OrderIndex.ShouldBe(1);
    }

    [Fact]
    public void ComponentConfig_deep_copy_does_not_share_lists_with_the_original()
    {
        // Same reason as Syllabus.DeepCopy: cloning a version while sharing lists means editing
        // the new copy also edits the old one.
        var original = new ComponentConfig
        {
            RecapFoundationTags = ["hooks"],
            InlineQuestions = [new InlineQuestion { Text = "Câu 1", Options = ["A", "B"], CorrectIndexes = [0] }],
        };

        var copy = original.DeepCopy();
        copy.RecapFoundationTags.Add("state");
        copy.InlineQuestions[0].Text = "Đã sửa";
        copy.InlineQuestions[0].Options.Add("C");

        original.RecapFoundationTags.Count.ShouldBe(1);
        original.InlineQuestions[0].Text.ShouldBe("Câu 1");
        original.InlineQuestions[0].Options.Count.ShouldBe(2);
    }
}
