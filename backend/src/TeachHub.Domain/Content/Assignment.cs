using TeachHub.Domain.Assignments;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Content;

public enum SubmissionMode { Individual, Group }

/// <summary>
/// Bài tập trong một phiên bản môn học. Cấu hình định dạng nộp KHÔNG khóa cứng vào ZIP mà
/// nằm ở danh sách <see cref="Slots"/> do giảng viên tự khai (SRS mục 5.4).
///
/// <c>SubjectVersionId</c> là bổ sung so với SRS (Gap #3) và là thứ BẮT BUỘC cho versioning:
/// không có nó thì Assignment thuộc về Subject chứ không thuộc về phiên bản, và clone
/// phiên bản sẽ dùng chung bài tập với năm cũ.
///
/// Ghi chú namespace: kiểu này ở lại <c>TeachHub.Domain.Content</c> chứ không chuyển sang
/// <c>TeachHub.Domain.Assignments</c> như plan Phase 3 mô tả. <c>AppDbContextModelSnapshot</c>
/// định danh entity bằng tên CLR đầy đủ, nên đổi namespace làm EF coi đây là entity mới và
/// sinh ra một cặp DROP TABLE + CREATE TABLE trên bảng đã có bài nộp thật.
/// </summary>
public class Assignment : Entity, ISoftDeletable
{
    public required Guid SubjectVersionId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    public SubmissionMode SubmissionMode { get; set; } = SubmissionMode.Individual;

    /// <summary>Chỉ có nghĩa khi <see cref="SubmissionMode"/> là Group — lưu jsonb.</summary>
    public GroupConfig? GroupConfig { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public bool AllowLateSubmission { get; set; }
    public decimal LatePenaltyPercentPerDay { get; set; }

    public Guid? RubricId { get; set; }
    public decimal MaxScore { get; set; } = 10m;

    public List<SubmissionSlot> Slots { get; set; } = [];
    public List<AssignmentHint> Hints { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Kiểm tra đã đủ điều kiện bấm "Nộp chính thức" chưa (SRS UC-08 bước 6).
    /// filled: (slot_key, số file/link đã có).
    /// </summary>
    public bool CanBeSubmittedWith(IReadOnlyCollection<(string SlotKey, int Count)> filled)
    {
        // Bài tập không còn slot nào là lỗi cấu hình — giảng viên xóa hết rồi quên thêm lại.
        // Cho nộp lúc này nghĩa là nhận một bài nộp rỗng mà không ai biết phải chấm cái gì.
        if (Slots.Count == 0) return false;

        var map = filled.ToDictionary(f => f.SlotKey, f => f.Count, StringComparer.OrdinalIgnoreCase);

        return Slots.Where(s => s.IsRequired)
                    .All(s => map.TryGetValue(s.SlotKey, out var n) && n > 0);
    }

    /// <summary>Các slot bắt buộc chưa có file nào — dùng để nói rõ còn thiếu gì.</summary>
    public IEnumerable<SubmissionSlot> MissingRequiredSlots(
        IReadOnlyCollection<(string SlotKey, int Count)> filled)
    {
        var map = filled.ToDictionary(f => f.SlotKey, f => f.Count, StringComparer.OrdinalIgnoreCase);

        return Slots.Where(s => s.IsRequired)
                    .Where(s => !map.TryGetValue(s.SlotKey, out var n) || n == 0);
    }

    /// <summary>Bản sao thuộc về phiên bản đích, Id mới hoàn toàn.</summary>
    public Assignment CloneTo(Guid targetVersionId) => new()
    {
        SubjectVersionId = targetVersionId,
        Title = Title,
        Description = Description,
        SubmissionMode = SubmissionMode,
        GroupConfig = GroupConfig?.DeepCopy(),

        // Hạn nộp KHÔNG clone là cố ý: sao chép hạn của năm ngoái sang năm nay tạo ra một
        // bài tập đã quá hạn ngay lúc vừa tạo, và sinh viên sẽ nhắn tin hỏi ngay ngày đầu.
        DueAt = null,

        AllowLateSubmission = AllowLateSubmission,
        LatePenaltyPercentPerDay = LatePenaltyPercentPerDay,
        RubricId = RubricId,             // rubric dùng chung được, không cần nhân bản
        MaxScore = MaxScore,
        Slots = [.. Slots.Select(s => s.CloneForNewOwner())],
        Hints = [.. Hints.Select(h => h.Clone())],
    };
}
