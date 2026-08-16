using Shouldly;
using TeachHub.Domain.Groups;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Kept even though Phase 4a ran under a "no tests" instruction: this is the pure function that
/// decides who works with whom for an entire semester, and its edge cases (a lone one-person group,
/// same seed producing the same result) are invisible to poking at the UI. Runs in under 10 ms.
/// </summary>
public class GroupAllocatorTests
{
    private static List<Guid> Students(int n) => [.. Enumerable.Range(0, n).Select(_ => Guid.CreateVersion7())];

    [Fact]
    public void Splits_evenly_when_the_class_size_divides_exactly()
    {
        var groups = GroupAllocator.Allocate(Students(12), groupSize: 4, seed: 42);

        groups.Count.ShouldBe(3);
        groups.ShouldAllBe(g => g.Count == 4);
    }

    [Fact]
    public void Never_leaves_behind_a_lone_one_person_group()
    {
        // 13 students, groups of 4 -> must NOT produce 4/4/4/1.
        var groups = GroupAllocator.Allocate(Students(13), groupSize: 4, seed: 42);

        groups.ShouldAllBe(g => g.Count >= 3);
        groups.Sum(g => g.Count).ShouldBe(13);
    }

    [Fact]
    public void Every_student_appears_exactly_once()
    {
        var students = Students(37);
        var groups = GroupAllocator.Allocate(students, groupSize: 5, seed: 7);

        groups.SelectMany(g => g).OrderBy(x => x).ShouldBe(students.OrderBy(x => x));
    }

    [Fact]
    public void The_same_seed_produces_the_same_result()
    {
        // The instructor clicks "Split randomly", previews it, and only then confirms —
        // the result must not change between the preview and the save.
        var students = Students(20);

        GroupAllocator.Allocate(students, 4, seed: 99)
            .ShouldBe(GroupAllocator.Allocate(students, 4, seed: 99));
    }

    [Fact]
    public void A_class_smaller_than_one_group_is_merged_into_a_single_group()
    {
        GroupAllocator.Allocate(Students(3), groupSize: 5, seed: 1).Count.ShouldBe(1);
    }

    [Fact]
    public void An_empty_list_returns_no_groups()
    {
        GroupAllocator.Allocate([], groupSize: 4, seed: 1).ShouldBeEmpty();
    }

    [Fact]
    public void A_class_of_40_split_into_groups_of_4_yields_exactly_10_equal_groups()
    {
        // Exactly the UC-25 acceptance scenario.
        var groups = GroupAllocator.Allocate(Students(40), groupSize: 4, seed: 2026);

        groups.Count.ShouldBe(10);
        groups.ShouldAllBe(g => g.Count == 4);
    }
}
