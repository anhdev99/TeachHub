using TeachHub.Domain.Common;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Domain.Content;

/// <summary>
/// Bài trắc nghiệm. Gap #3: thuộc về phiên bản môn HOẶC một lớp cụ thể, đúng MỘT trong hai —
/// có CHECK constraint canh giữ. Quiz thuộc phiên bản môn thì được clone theo version; quiz
/// thuộc lớp là bài kiểm tra riêng của lớp đó và không dính sang năm sau.
///
/// Bỏ CHECK ở đây thì một quiz thuộc cả hai nơi cùng lúc, và clone version sẽ nhân đôi nó.
/// </summary>
public class Quiz : Entity, ISoftDeletable
{
    public Guid? SubjectVersionId { get; set; }
    public Guid? OfferingId { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Cấu hình chấm và thời gian — lưu jsonb.</summary>
    public QuizConfig Config { get; set; } = new();

    public List<QuizItem> Items { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Bản sao thuộc phiên bản đích. Chỉ clone quiz gắn với phiên bản môn — quiz riêng của
    /// một lớp không có lý do gì đi theo đề cương sang năm sau.
    /// </summary>
    public Quiz CloneTo(Guid targetVersionId) => new()
    {
        SubjectVersionId = targetVersionId,
        OfferingId = null,
        Title = Title,
        Description = Description,
        Config = Config.DeepCopy(),
    };
}
