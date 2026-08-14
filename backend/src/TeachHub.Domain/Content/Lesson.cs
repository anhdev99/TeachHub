using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

public enum LessonContentType { Ppt, Pdf, VideoEmbed, Text, Interactive }

public class Lesson : Entity, ISoftDeletable
{
    public Guid ChapterId { get; set; }
    public int OrderIndex { get; set; }
    public required string Title { get; set; }
    public LessonContentType ContentType { get; set; }
    public string? FileUrl { get; set; }
    public string? TextContent { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>Giảng viên bật/tắt cho TỪNG bài — không phải cài đặt toàn cục (SRS mục 3.2).</summary>
    public bool IsRequiredToUnlockNext { get; set; }

    public List<LessonComponent> Components { get; private set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Không có ràng buộc nào bắt Lesson phải có component (SRS mục 5.1).</summary>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Title);

    public LessonComponent AddComponent(LessonComponentType type, Guid? refId, ComponentConfig config)
    {
        if (LessonComponent.RequiresRef(type) && refId is null)
            throw new ArgumentException($"Component loại {type} bắt buộc phải trỏ tới một bản ghi.", nameof(refId));
        if (!LessonComponent.RequiresRef(type) && refId is not null)
            throw new ArgumentException($"Component loại {type} mang nội dung nhúng, không được có ref_id.", nameof(refId));

        var c = new LessonComponent
        {
            LessonId = Id, Type = type, RefId = refId,
            Config = config, OrderIndex = Components.Count,
        };
        Components.Add(c);
        return c;
    }

    public void RemoveComponent(Guid componentId)
    {
        var c = Components.SingleOrDefault(x => x.Id == componentId)
                ?? throw new InvalidOperationException($"Không có component {componentId} trong bài này.");
        Components.Remove(c);
        Reindex();
    }

    public void MoveComponent(Guid componentId, int toIndex)
    {
        var c = Components.SingleOrDefault(x => x.Id == componentId)
                ?? throw new InvalidOperationException($"Không có component {componentId} trong bài này.");

        Components.Remove(c);
        Components.Insert(Math.Clamp(toIndex, 0, Components.Count), c);
        Reindex();
    }

    // OrderIndex phải luôn là 0..n-1 liên tục. Nếu để hở, kéo-thả ở UI sẽ
    // dần tạo ra các khoảng trống và thứ tự hiển thị thành không xác định.
    private void Reindex()
    {
        for (var i = 0; i < Components.Count; i++) Components[i].OrderIndex = i;
    }
}
