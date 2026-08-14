using TeachHub.Domain.Content;

namespace TeachHub.Application.Features.Content;

public sealed record ChapterDto(
    Guid Id, Guid SubjectVersionId, string Title, string? Objective,
    int OrderIndex, IReadOnlyList<string> FoundationTags, UnlockRule UnlockRule);

public sealed record LessonComponentDto(
    Guid Id, Guid LessonId, LessonComponentType Type, int OrderIndex,
    Guid? RefId, bool IsRequired, ComponentConfig Config);

public sealed record LessonDto(
    Guid Id, Guid ChapterId, string Title, int OrderIndex,
    LessonContentType ContentType, string? FileUrl, string? TextContent,
    int DurationMinutes, bool IsRequiredToUnlockNext,
    IReadOnlyList<LessonComponentDto> Components);
