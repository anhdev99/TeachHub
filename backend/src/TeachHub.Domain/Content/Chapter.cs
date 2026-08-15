using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

/// <summary>
/// Quy tắc mở khóa của chương. <c>Free</c> = học tự do; <c>AfterPreviousCompleted</c> = phải
/// hoàn thành chương trước. Engine mở khóa thật nằm ở Phase 2, ở đây chỉ lưu cấu hình.
/// </summary>
public enum UnlockRule { Free, AfterPreviousCompleted }

public class Chapter : Entity, ISoftDeletable
{
    public Guid SubjectVersionId { get; set; }
    public int OrderIndex { get; set; }
    public required string Title { get; set; }
    public string? Objective { get; set; }

    /// <summary>Nhãn kiến thức nền, dùng cho recap và ôn tập ngắt quãng — cột <c>text[]</c>.</summary>
    public List<string> FoundationTags { get; set; } = [];

    public UnlockRule UnlockRule { get; set; } = UnlockRule.Free;

    public List<Lesson> Lessons { get; private set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Lesson AddLesson(Lesson lesson)
    {
        lesson.ChapterId = Id;
        lesson.OrderIndex = Lessons.Count;
        Lessons.Add(lesson);
        return lesson;
    }

    public void RemoveLesson(Guid lessonId)
    {
        var l = Lessons.SingleOrDefault(x => x.Id == lessonId)
                ?? throw new InvalidOperationException($"Không có bài {lessonId} trong chương này.");
        Lessons.Remove(l);
        Reindex();
    }

    public void MoveLesson(Guid lessonId, int toIndex)
    {
        var l = Lessons.SingleOrDefault(x => x.Id == lessonId)
                ?? throw new InvalidOperationException($"Không có bài {lessonId} trong chương này.");

        SapTheoOrderIndex();
        Lessons.Remove(l);
        Lessons.Insert(Math.Clamp(toIndex, 0, Lessons.Count), l);
        DanhSoLai();
    }

    // Cùng lý do với Lesson.Reindex: OrderIndex hở là thứ tự hiển thị thành không xác định.
    private void Reindex()
    {
        SapTheoOrderIndex();
        DanhSoLai();
    }

    // Xem Lesson.SapTheoOrderIndex: thứ tự EF nạp về không phải OrderIndex.
    private void SapTheoOrderIndex() => Lessons.Sort((x, y) => x.OrderIndex.CompareTo(y.OrderIndex));

    private void DanhSoLai()
    {
        for (var i = 0; i < Lessons.Count; i++) Lessons[i].OrderIndex = i;
    }
}
