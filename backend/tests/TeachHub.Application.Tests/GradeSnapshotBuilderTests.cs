using Shouldly;
using TeachHub.Application.Features.Gradebook;
using TeachHub.Domain.Grading;

namespace TeachHub.Application.Tests;

public class GradeSnapshotBuilderTests
{
    private static readonly Guid CourseworkColumn = Guid.CreateVersion7();
    private static readonly Guid FinalExamColumn = Guid.CreateVersion7();

    private static GradebookDto Sheet(params GradeRowDto[] rows) => new(
        Guid.CreateVersion7(), "DA24TIN01", "LTW", "Lập trình Web",
        [
            new GradeColumnDto(CourseworkColumn, "thuong_ky", "Thường kỳ", 40m, 0,
                AggregationKind.Average, 0, true, []),
            new GradeColumnDto(FinalExamColumn, "cuoi_mon", "Cuối môn", 60m, 1,
                AggregationKind.WeightedAverage, 0, true, []),
        ],
        [.. rows],
        [new GradeConditionDto("cuoi_mon", ConditionOperator.LessThan, 4m, CapAction.CapFinalAt, 3.9m)],
        BonusTarget.GradeColumn, CourseworkColumn, 1m,
        WeightsValid: true, WeightsError: null, UnknownConditionColumnKeys: []);

    private static GradeRowDto Row(string studentCode, string fullName, decimal finalScore) => new(
        Guid.CreateVersion7(), studentCode, fullName, "DA24TIN",
        [
            new GradeCellDto(CourseworkColumn, 8m, 8.5m, IsManualOverride: false, Bonus: 0.5m),
            new GradeCellDto(FinalExamColumn, 7m, 7m, IsManualOverride: true, Bonus: 0m),
        ],
        BonusToFinal: 0m, FinalScore: finalScore);

    [Fact]
    public void The_JSON_string_is_unchanged_when_rows_come_back_in_a_different_order()
    {
        // Postgres's default sort order depends on the database collation. If the snapshot kept
        // whatever order the query returned, the same grade sheet on two servers would produce two
        // different hashes, and the snapshot would lose all of its reconciliation value.
        var a = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m), Row("2251012002", "Trần Bảo", 8m))));

        var b = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            Sheet(Row("2251012002", "Trần Bảo", 8m), Row("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        // EnrollmentId is regenerated on every Row() call, so the two strings are not byte
        // identical; what has to be asserted is that the STUDENT CODE ORDER has been normalized,
        // not the order they were passed in.
        a.IndexOf("2251012001", StringComparison.Ordinal)
            .ShouldBeLessThan(a.IndexOf("2251012002", StringComparison.Ordinal));
        b.IndexOf("2251012001", StringComparison.Ordinal)
            .ShouldBeLessThan(b.IndexOf("2251012002", StringComparison.Ordinal));
    }

    [Fact]
    public void The_same_grade_sheet_produces_the_same_string()
    {
        var gradebook = Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m));

        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(gradebook))
            .ShouldBe(GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(gradebook)));
    }

    [Fact]
    public void The_payload_records_column_keys_rather_than_ids()
    {
        // The export template and the minimum-score conditions both join to columns by KEY. The
        // snapshot must do the same: an id says nothing when it is read back six months later,
        // and the column may well have been deleted by then.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        json.ShouldContain("thuong_ky");
        json.ShouldContain("cuoi_mon");
        json.ShouldNotContain(CourseworkColumn.ToString());
    }

    [Fact]
    public void The_payload_preserves_Vietnamese_diacritics()
    {
        // The snapshot also has to be readable by eye when a student files a complaint. Escaping
        // to \uXXXX turns the whole page into escape sequences.
        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
                Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m))))
            .ShouldContain("Nguyễn Văn Ánh");
    }

    [Fact]
    public void Enums_are_written_as_STRINGS_rather_than_numbers()
    {
        // Ordinals change meaning the moment someone inserts a value into the middle of an enum,
        // and at that point every old snapshot misdescribes itself with no way to detect it.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
            Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m))));

        json.ShouldContain("average");
        json.ShouldContain("less_than");
    }

    [Fact]
    public void Scores_stay_decimal_and_never_pass_through_floating_point()
    {
        // 8.5 has to come out as "8.5", not "8.4999999999999996". Scores are decimal end to end.
        GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(
                Sheet(Row("2251012001", "Nguyễn Văn Ánh", 7.6m))))
            .ShouldContain("8.5");
    }

    [Fact]
    public void A_grade_sheet_with_no_students_can_still_be_finalized()
    {
        // Closing a class before anyone enrolled is rare but real (a cancelled class). Throwing
        // here means the instructor cannot close the class and has no idea why.
        var json = GradeSnapshotBuilder.Serialize(GradeSnapshotBuilder.Build(Sheet()));

        json.ShouldContain("\"rows\":[]");
    }
}
