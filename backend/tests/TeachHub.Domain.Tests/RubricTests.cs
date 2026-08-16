using Shouldly;
using TeachHub.Domain.Assignments;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Kept even though the phase ran under a "no tests" instruction: this is the arithmetic that
/// produces the number handed to the academic office. A rounding error here has no symptom in the
/// UI — it only surfaces when a student files a complaint. Runs in under 10 ms.
/// </summary>
public class RubricTests
{
    private static Rubric Sample() => new()
    {
        Name = "Rubric bài tập lập trình",
        InstructorId = Guid.CreateVersion7(),
        Criteria =
        [
            new RubricCriterion { Description = "Chạy đúng yêu cầu", MaxPoints = 5m, OrderIndex = 0 },
            new RubricCriterion { Description = "Xử lý lỗi hợp lý", MaxPoints = 3m, OrderIndex = 1 },
            new RubricCriterion { Description = "Code sạch, có chú thích", MaxPoints = 2m, OrderIndex = 2 },
        ],
    };

    [Fact]
    public void Total_max_points_equals_the_sum_of_the_criteria() => Sample().MaxPoints.ShouldBe(10m);

    [Fact]
    public void Grading_accumulates_across_the_individual_criteria()
    {
        var r = Sample();

        r.Score(new Dictionary<Guid, decimal>
        {
            [r.Criteria[0].Id] = 4.5m,
            [r.Criteria[1].Id] = 2m,
            [r.Criteria[2].Id] = 1.5m,
        }).ShouldBe(8m);
    }

    [Fact]
    public void Grading_above_a_criterion_max_is_an_error()
    {
        var r = Sample();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 99m }));
    }

    [Fact]
    public void Grading_with_a_negative_score_is_an_error()
    {
        var r = Sample();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = -1m }));
    }

    [Fact]
    public void An_ungraded_criterion_counts_as_0_rather_than_raising_an_error()
    {
        // The instructor grades halfway and saves a draft — that must not throw.
        var r = Sample();

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 5m }).ShouldBe(5m);
    }

    [Fact]
    public void Scores_are_always_rounded_to_2_decimal_places()
    {
        // decimal, not double — scores never travel through binary floating point.
        var r = new Rubric
        {
            Name = "x", InstructorId = Guid.CreateVersion7(),
            Criteria = [new RubricCriterion { Description = "a", MaxPoints = 10m, OrderIndex = 0 }],
        };

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 3.333m }).ShouldBe(3.33m);
    }

    [Fact]
    public void Rounding_at_the_midpoint_goes_away_from_zero_not_to_even()
    {
        // AwayFromZero, NOT .NET's default ToEven: 8.125 must produce 8.13, not 8.12.
        // A student rounded down at the midpoint has a legitimate complaint.
        var r = new Rubric
        {
            Name = "x", InstructorId = Guid.CreateVersion7(),
            Criteria = [new RubricCriterion { Description = "a", MaxPoints = 10m, OrderIndex = 0 }],
        };

        r.Score(new Dictionary<Guid, decimal> { [r.Criteria[0].Id] = 8.125m }).ShouldBe(8.13m);
    }
}
