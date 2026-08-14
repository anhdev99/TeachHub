using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Subjects;

public sealed record SubjectDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid? CurrentVersionId,
    DateTimeOffset CreatedAt)
{
    public static SubjectDto From(Subject s) =>
        new(s.Id, s.Code, s.Name, s.Description, s.CurrentVersionId, s.CreatedAt);
}

/// <summary>
/// <c>Status</c> giữ nguyên kiểu enum để bộ chuyển đổi JSON toàn cục sinh ra chuỗi
/// snake_case (<c>draft</c>/<c>published</c>/<c>archived</c>) đúng hợp đồng SRS mục 7.
/// Đổi sang <c>string</c> ở đây là tự tay ghép chuỗi và sớm muộn sẽ lệch.
/// </summary>
public sealed record SubjectVersionDto(
    Guid Id,
    Guid SubjectId,
    string VersionLabel,
    SubjectVersionStatus Status,
    Guid? ClonedFromVersionId,
    string? Changelog,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt)
{
    public static SubjectVersionDto From(SubjectVersion v) =>
        new(v.Id, v.SubjectId, v.VersionLabel, v.Status, v.ClonedFromVersionId,
            v.Changelog, v.PublishedAt, v.CreatedAt);
}
