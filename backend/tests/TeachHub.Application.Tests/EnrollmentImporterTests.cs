using Shouldly;
using TeachHub.Application.Features.Enrollments;

namespace TeachHub.Application.Tests;

public class EnrollmentImporterTests
{
    private static ImportRow Row(string code, string fullName, string email) => new(code, fullName, email, null);

    [Fact]
    public void Valid_rows_import_successfully()
    {
        var result = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
        ]);

        result.Valid.Count.ShouldBe(2);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void A_duplicate_student_code_within_one_file_reports_the_right_row_number()
    {
        var result = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
            Row("2251012001", "Nguyễn Văn A", "a2@sv.edu.vn"),   // row 4 in Excel
        ]);

        result.Valid.Count.ShouldBe(2);                    // the 2 good rows are STILL imported
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].RowNumber.ShouldBe(4);            // 1-based, plus 1 header row
        result.Errors[0].Message.ShouldContain("2251012001");
    }

    [Fact]
    public void A_student_code_Excel_turned_into_a_number_is_still_normalized()
    {
        // Excel converts "2251012001" into a number and then exports it as "2251012001.0",
        // depending on the version. This is the single most common import failure.
        var result = EnrollmentImporter.Validate([Row("2251012001.0", "Nguyễn Văn A", "a@sv.edu.vn")]);

        result.Valid.Single().StudentCode.ShouldBe("2251012001");
    }

    [Fact]
    public void Extra_whitespace_is_trimmed_and_the_email_is_lowercased()
    {
        var result = EnrollmentImporter.Validate([Row("  2251012001  ", " Nguyễn  Văn A ", "  A@SV.EDU.VN ")]);

        var student = result.Valid.Single();
        student.StudentCode.ShouldBe("2251012001");
        student.FullName.ShouldBe("Nguyễn Văn A");    // interior whitespace is collapsed too
        student.Email.ShouldBe("a@sv.edu.vn");
    }

    [Fact]
    public void A_fully_blank_row_is_skipped_without_reporting_an_error()
    {
        // Real files often carry a few trailing blank rows because the user selected too far.
        var result = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("", "", ""),
        ]);

        result.Valid.Count.ShouldBe(1);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_student_code_or_full_name_reports_an_error_on_that_row()
    {
        var result = EnrollmentImporter.Validate([
            Row("", "Nguyễn Văn A", "a@sv.edu.vn"),
            Row("2251012002", "", "b@sv.edu.vn"),
        ]);

        result.Errors.Count.ShouldBe(2);
        result.Errors.Select(e => e.RowNumber).ShouldBe([2, 3]);
    }

    [Fact]
    public void A_malformed_email_reports_an_error_without_blocking_the_whole_file()
    {
        var result = EnrollmentImporter.Validate([
            Row("2251012001", "Nguyễn Văn A", "khong-phai-email"),
            Row("2251012002", "Trần Thị B",   "b@sv.edu.vn"),
        ]);

        result.Valid.Count.ShouldBe(1);
        result.Errors.Single().RowNumber.ShouldBe(2);
    }

    [Fact]
    public void A_missing_email_still_imports_because_email_is_optional()
    {
        // Rosters from the academic office frequently lack emails; the student can still be
        // matched into the class by student code.
        var result = EnrollmentImporter.Validate([Row("2251012001", "Nguyễn Văn A", "")]);

        result.Errors.ShouldBeEmpty();
        result.Valid.Single().Email.ShouldBe("");
    }

    [Fact]
    public void Acceptance_40_rows_with_2_duplicates_and_1_missing_name_imports_37()
    {
        var rows = Enumerable.Range(1, 40)
            .Select(i => Row($"225101{i:D4}", $"Sinh viên {i}", $"sv{i}@sv.edu.vn"))
            .ToList();

        rows[10] = Row("2251010001", "Trùng thứ nhất", "x1@sv.edu.vn");   // duplicates row 1
        rows[20] = Row("2251010002", "Trùng thứ hai",  "x2@sv.edu.vn");   // duplicates row 2
        rows[30] = Row("2251010099", "",               "x3@sv.edu.vn");   // missing full name

        var result = EnrollmentImporter.Validate(rows);

        result.Valid.Count.ShouldBe(37);
        result.Errors.Count.ShouldBe(3);
        result.Errors.Select(e => e.RowNumber).ShouldBe([12, 22, 32]);
    }
}
