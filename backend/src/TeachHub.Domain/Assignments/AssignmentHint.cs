using TeachHub.Domain.Common;

namespace TeachHub.Domain.Assignments;

/// <summary>
/// Gợi ý mở dần cho bài tập (SRS UC-09). Gap #11: SRS khai hints là mảng jsonb trên
/// Assignment, nhưng mỗi lần sinh viên mở một gợi ý là một sự kiện phải ghi lại được
/// (ai mở, lúc nào, trừ bao nhiêu điểm) — mảng jsonb không gắn khóa ngoại vào đâu được.
/// Tách thành bảng riêng ngay từ đầu rẻ hơn nhiều so với migrate lúc đã có dữ liệu.
/// </summary>
public class AssignmentHint : Entity
{
    public Guid AssignmentId { get; set; }
    public int OrderIndex { get; set; }
    public required string Content { get; set; }

    /// <summary>Phần trăm điểm bị trừ khi mở gợi ý này. 0 nghĩa là mở miễn phí.</summary>
    public decimal PenaltyPercent { get; set; }

    public AssignmentHint Clone() => new()
    {
        OrderIndex = OrderIndex,
        Content = Content,
        PenaltyPercent = PenaltyPercent,
    };
}
