using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum GradeExportSource
{
    StudentCode,
    FullName,
    Email,
    ClassName,
    GradeColumn,
    BonusTotal,
    FinalScore,
    Blank,           // an empty column for the registrar's office to fill in
}

/// <summary>
/// The export template of ONE instructor, reusable across every class that shares the same set
/// of keys. The template belongs to the instructor while the grade columns belong to the class —
/// the two sides are linked by KEY, not by id. Link by id and a carefully built template only
/// works for one single class; in the next class every column has a new id and the export comes
/// out empty, so the user has to rebuild the template per class and the feature might as well
/// not exist.
/// </summary>
public class GradeExportTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public List<GradeExportItem> Items { get; set; } = [];
}

public class GradeExportItem : Entity
{
    public Guid TemplateId { get; set; }
    public required GradeExportSource Source { get; set; }

    /// <summary>Used only when Source = GradeColumn. It is the column's KEY, NOT its id.</summary>
    public string? ColumnKey { get; set; }

    /// <summary>The heading shown in the file — editable so it can match the university's own grade sheet.</summary>
    public required string Header { get; set; }

    public int OrderIndex { get; set; }
    public int DecimalPlaces { get; set; } = 1;
}

/// <summary>
/// The immutable snapshot taken when a class moves to <c>Closed</c> (the exception to ADR-006's
/// "never store the final total"). Append-only: no updates, no deletes — not even when the class
/// is reopened and then closed a second time.
/// </summary>
public class GradeSnapshot : Entity
{
    public required Guid OfferingId { get; set; }
    public required DateTimeOffset TakenAt { get; set; }
    public required Guid TakenBy { get; set; }

    /// <summary>The whole gradebook at snapshot time: columns, weights, aggregation rules, conditions, every cell, the totals.</summary>
    public required string PayloadJson { get; set; }

    /// <summary>SHA-256 of PayloadJson — so a later comparison does not have to rely on anyone's memory.</summary>
    public required string PayloadHash { get; set; }
}
