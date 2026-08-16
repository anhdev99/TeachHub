using System.Globalization;
using System.Text.RegularExpressions;

namespace TeachHub.Application.Features.Enrollments;

public sealed record ImportRow(string StudentCode, string FullName, string Email, string? ClassName);
public sealed record ImportError(int RowNumber, string Message);
public sealed record ValidatedStudent(string StudentCode, string FullName, string Email, string? ClassName);
public sealed record ImportResult(List<ValidatedStudent> Valid, List<ImportError> Errors);

public static partial class EnrollmentImporter
{
    /// <summary>
    /// Import as much as can be imported. One broken row must not spoil the whole file — the
    /// instructor only has to fix exactly the rows that were listed and then import again. An
    /// all-or-nothing failure over a single bad row means editing the file by hand before every try.
    /// </summary>
    public static ImportResult Validate(IReadOnlyList<ImportRow> rows)
    {
        var valid = new List<ValidatedStudent>();
        var errors = new List<ImportError>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2;   // +1 because 1-based, +1 because of the header row
            var r = rows[i];

            var code = NormalizeStudentCode(r.StudentCode);
            var name = NormalizeName(r.FullName);
            var email = r.Email?.Trim().ToLowerInvariant() ?? "";

            if (code.Length == 0 && name.Length == 0 && email.Length == 0)
                continue;                                    // blank row: skip silently

            if (code.Length == 0) { errors.Add(new(rowNumber, "Thiếu MSSV.")); continue; }
            if (name.Length == 0) { errors.Add(new(rowNumber, $"Thiếu họ tên (MSSV {code}).")); continue; }

            // Email is optional: the registrar's roster is often missing it, and a student can
            // still be matched into the class by student code. But if present it must be well-formed.
            if (email.Length > 0 && !EmailPattern().IsMatch(email))
            { errors.Add(new(rowNumber, $"Email không hợp lệ: '{r.Email}'.")); continue; }

            if (!seen.Add(code))
            { errors.Add(new(rowNumber, $"MSSV {code} bị trùng với một dòng phía trên.")); continue; }

            valid.Add(new ValidatedStudent(code, name, email, r.ClassName?.Trim()));
        }

        return new ImportResult(valid, errors);
    }

    /// <summary>
    /// Excel likes to turn a student code into a number and then export it as "2251012001.0"
    /// or in scientific notation. Restore it to the original digit string.
    /// </summary>
    private static string NormalizeStudentCode(string? raw)
    {
        var s = raw?.Trim() ?? "";
        if (s.Length == 0) return "";

        if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d == Math.Floor(d))
            return ((long)d).ToString(CultureInfo.InvariantCulture);

        return s;
    }

    private static string NormalizeName(string? raw) =>
        WhitespaceRuns().Replace(raw?.Trim() ?? "", " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
