using Shouldly;
using TeachHub.Domain.Content;

namespace TeachHub.Domain.Tests;

public class SubjectVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    private static SubjectVersion Draft(string label = "2026-2027") =>
        SubjectVersion.CreateDraft(Guid.CreateVersion7(), label, Now);

    [Fact]
    public void A_newly_created_version_is_always_in_draft_status()
    {
        Draft().Status.ShouldBe(SubjectVersionStatus.Draft);
    }

    [Fact]
    public void Only_a_draft_can_have_its_content_edited()
    {
        var v = Draft();
        v.CanEditContent.ShouldBeTrue();

        v.Publish(Now);
        v.CanEditContent.ShouldBeFalse();   // published is immutable

        v.Archive(Now);
        v.CanEditContent.ShouldBeFalse();
    }

    [Fact]
    public void Publish_records_the_publication_timestamp()
    {
        var v = Draft();
        v.Publish(Now);

        v.Status.ShouldBe(SubjectVersionStatus.Published);
        v.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Publishing_twice_is_an_error()
    {
        var v = Draft();
        v.Publish(Now);

        Should.Throw<InvalidOperationException>(() => v.Publish(Now));
    }

    [Fact]
    public void An_archived_version_cannot_be_published()
    {
        // To reuse old content you must clone it into a new draft, never "resurrect" the old
        // version — otherwise the classes still referencing it change meaning underneath them.
        var v = Draft();
        v.Publish(Now);
        v.Archive(Now);

        Should.Throw<InvalidOperationException>(() => v.Publish(Now));
    }

    [Fact]
    public void Archiving_a_draft_is_an_error()
    {
        Should.Throw<InvalidOperationException>(() => Draft().Archive(Now));
    }

    [Fact]
    public void The_version_label_must_not_be_blank()
    {
        Should.Throw<ArgumentException>(() => Draft("   "));
    }

    [Fact]
    public void A_clone_remembers_its_source_and_is_still_a_draft()
    {
        var source = Guid.CreateVersion7();
        var v = SubjectVersion.CreateDraft(Guid.CreateVersion7(), "2027-2028", Now, clonedFrom: source);

        v.Status.ShouldBe(SubjectVersionStatus.Draft);
        v.ClonedFromVersionId.ShouldBe(source);
    }

    [Fact]
    public void Syllabus_deep_copy_does_not_share_lists_with_the_original()
    {
        // If DeepCopy only assigned references, editing the learning outcomes on the 2027-2028
        // version would also change the syllabus of the already-taught 2026-2027 class —
        // that is corrupted historical data, not a display bug.
        var original = new Syllabus
        {
            Credits = 3,
            Outcomes = [new LearningOutcome { Code = "CĐR1.1", Text = "Dựng được ứng dụng" }],
            References = ["React Native docs"],
        };

        var copy = original.DeepCopy();
        copy.Outcomes.Add(new LearningOutcome { Code = "CĐR1.2", Text = "Thêm ở bản mới" });
        copy.References.Add("Sách mới");
        copy.Outcomes[0].Text = "Đã sửa";

        original.Outcomes.Count.ShouldBe(1);
        original.References.Count.ShouldBe(1);
        original.Outcomes[0].Text.ShouldBe("Dựng được ứng dụng");
    }
}
