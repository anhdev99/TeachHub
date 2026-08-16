using TeachHub.Application.Features.Enrollments;

namespace TeachHub.Application.Common;

/// <summary>
/// Result of reading a roster file. <c>Headers</c> keeps the column names that were actually
/// found so the error can be useful: "no student-id column found, the columns read were …"
/// instead of a vague failure.
/// </summary>
public sealed record RosterReadResult(List<ImportRow> Rows, List<string> Headers, string? Error);

/// <summary>
/// Port for reading a student roster file. Application has no idea ClosedXML exists — which is
/// why a handler test only needs a fake implementation instead of building a real .xlsx.
/// </summary>
public interface IEnrollmentReader
{
    RosterReadResult Read(Stream content);
}
