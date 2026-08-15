using TeachHub.Domain.Grading;

namespace TeachHub.Application.Features.Gradebook;

public sealed record GradeColumnSourceDto(Guid Id, GradeSourceType SourceType, Guid SourceId,
    string SourceTitle, decimal? WeightWithinColumn);

public sealed record GradeColumnDto(
    Guid Id, string Key, string Name, decimal WeightPercent, int OrderIndex,
    AggregationKind Kind, int N, bool TreatMissingAsZero,
    List<GradeColumnSourceDto> Sources);

public sealed record GradeCellDto(
    Guid ColumnId, decimal RawScore, decimal Score, bool IsManualOverride, decimal Bonus);

public sealed record GradeRowDto(
    Guid EnrollmentId, string StudentCode, string FullName, string? ClassName,
    List<GradeCellDto> Cells, decimal BonusToFinal, decimal FinalScore);

public sealed record GradeConditionDto(string ColumnKey, ConditionOperator Op,
    decimal Threshold, CapAction Action, decimal ActionValue);

public sealed record GradebookDto(
    Guid OfferingId, string ClassCode, string SubjectCode, string SubjectName,
    List<GradeColumnDto> Columns, List<GradeRowDto> Rows,
    List<GradeConditionDto> Conditions,
    BonusTarget BonusDefaultTarget, Guid? BonusDefaultColumnId,
    decimal? BonusMaxPointsPerStudent,
    bool WeightsValid, string? WeightsError,
    List<string> UnknownConditionColumnKeys);

public sealed record BonusPointDto(
    Guid Id, Guid EnrollmentId, string StudentName, BonusTarget Target, Guid? TargetColumnId,
    decimal Points, string Reason, DateTimeOffset GrantedAt);

public sealed record BonusReasonTemplateDto(
    Guid Id, string Label, decimal DefaultPoints, BonusTarget? DefaultTarget,
    string? DefaultColumnKey, int OrderIndex, bool IsActive);

public sealed record GradeSchemeTemplateItemDto(
    string Key, string Name, decimal WeightPercent, int OrderIndex,
    AggregationKind Kind, int N, bool TreatMissingAsZero);

public sealed record GradeSchemeTemplateDto(
    Guid Id, string Name, bool IsDefault, int OrderIndex, List<GradeSchemeTemplateItemDto> Items);

public sealed record GradeExportItemDto(
    GradeExportSource Source, string? ColumnKey, string Header, int OrderIndex, int DecimalPlaces);

public sealed record GradeExportTemplateDto(
    Guid Id, string Name, bool IsDefault, List<GradeExportItemDto> Items);
