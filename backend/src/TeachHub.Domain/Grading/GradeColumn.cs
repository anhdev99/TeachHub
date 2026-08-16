using TeachHub.Domain.Common;

namespace TeachHub.Domain.Grading;

public enum GradeSourceType { Assignment, Quiz, FinalAssessment }

/// <summary>
/// A grade column of ONE class. It is COPIED from <see cref="GradeSchemeTemplate"/> when the
/// class is opened and then lives on its own: editing the template in a later term must NOT
/// change the grade columns of a class that has already been taught.
///
/// Same principle as ADR-004 (a class pins its SubjectVersion). If the column were only a
/// pointer to the template, changing that template from 20/30/50 to 30/20/50 would also change
/// the already-submitted gradebook of the previous term — nobody pressed a button, and nothing
/// in the system would tell you.
/// </summary>
public class GradeColumn : Entity
{
    public required Guid OfferingId { get; set; }

    /// <summary>
    /// A stable slug (<c>thuong_ky</c>, <c>cuoi_mon</c>). Excel export templates, minimum-score
    /// conditions and the bonus-reason library all link to the column through this KEY, never
    /// through the Id: they belong to the instructor while the column belongs to the class, so
    /// linking by id would make each template usable in exactly one class — which is the same
    /// as the feature not existing.
    /// </summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public required decimal WeightPercent { get; set; }
    public int OrderIndex { get; set; }
    public GradeAggregation Aggregation { get; set; } = new();
    public List<GradeColumnSource> Sources { get; set; } = [];
}

public class GradeColumnSource : Entity
{
    public required Guid GradeColumnId { get; set; }
    public required GradeSourceType SourceType { get; set; }
    public required Guid SourceId { get; set; }

    /// <summary>Weight of this source WITHIN the column, used only when Kind = WeightedAverage. Null = split evenly.</summary>
    public decimal? WeightWithinColumn { get; set; }
}

/// <summary>One student's score in one column. The final total is stored NOWHERE (ADR-006).</summary>
public class GradeRecord : Entity
{
    public required Guid EnrollmentId { get; set; }
    public required Guid GradeColumnId { get; set; }
    public decimal? Score { get; set; }

    /// <summary>
    /// true = the instructor edited this cell by hand, and re-grading a submission inside the
    /// column must NOT overwrite it. Without this flag an instructor's adjusted score silently
    /// disappears on the next re-grade, and nobody notices until the student asks.
    /// </summary>
    public bool IsManualOverride { get; set; }

    public string? Note { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// The library of column-set templates of ONE instructor. It hangs off the instructor, not off
/// the subject: write it once, use it for every subject and every term thereafter.
/// </summary>
public class GradeSchemeTemplate : Entity
{
    public required Guid InstructorId { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public int OrderIndex { get; set; }
    public List<GradeSchemeTemplateItem> Items { get; set; } = [];
}

public class GradeSchemeTemplateItem : Entity
{
    public Guid TemplateId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public required decimal WeightPercent { get; set; }
    public int OrderIndex { get; set; }
    public GradeAggregation Aggregation { get; set; } = new();
}

/// <summary>
/// The default column set for an instructor who has not written a template yet. Two columns is
/// the most common split in Vietnamese universities, and shipping a set that already works
/// matters more than forcing the user to configure one before they ever see a gradebook.
/// </summary>
public static class GradeColumnFactory
{
    public static List<GradeColumn> MacDinh(Guid offeringId) =>
    [
        new()
        {
            OfferingId = offeringId, Key = "thuong_ky", Name = "Thường kỳ",
            WeightPercent = 40m, OrderIndex = 0,
            Aggregation = new GradeAggregation { Kind = AggregationKind.Average },
        },
        new()
        {
            OfferingId = offeringId, Key = "cuoi_mon", Name = "Cuối môn",
            WeightPercent = 60m, OrderIndex = 1,
            Aggregation = new GradeAggregation { Kind = AggregationKind.Average },
        },
    ];
}
