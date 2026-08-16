using TeachHub.Domain.Common;

namespace TeachHub.Domain.Quizzes;

public enum QuestionType { SingleChoice, MultipleChoice, TrueFalse, FillBlank }

/// <summary>
/// Câu hỏi trong ngân hàng của MỘT giảng viên. Gắn vào giảng viên chứ không vào môn để tái
/// sử dụng qua nhiều môn và nhiều học kỳ — cùng lý do với Rubric (Gap #18).
/// </summary>
public class QuizQuestion : Entity
{
    public required Guid InstructorId { get; set; }
    public required QuestionType Type { get; set; }
    public required string Text { get; set; }
    public string? Explanation { get; set; }

    /// <summary>
    /// Nhãn kiến thức nền, dùng cho component <c>recap</c> và hàng đợi ôn tập (Phase 8):
    /// "chọn 3–5 câu thuộc tag của chương trước".
    /// </summary>
    public List<string> FoundationTags { get; set; } = [];

    /// <summary>Điểm của câu này trong một quiz. Thang tự nhiên của quiz là tổng điểm các câu.</summary>
    public decimal Points { get; set; } = 1m;

    public List<QuestionOption> Options { get; set; } = [];

    /// <summary>Chỉ có nghĩa với <see cref="QuestionType.FillBlank"/> — lưu jsonb.</summary>
    public FillBlankConfig? FillBlank { get; set; }

    /// <summary>
    /// Chấm một phần cho <see cref="QuestionType.MultipleChoice"/>. Mặc định FALSE —
    /// tất-cả-hoặc-không, vì đó là luật dễ giải thích cho sinh viên nhất. Để cấu hình được
    /// theo từng câu thay vì khóa cứng toàn hệ thống: hai kiểu chấm đều chính đáng, và chọn
    /// cứng một kiểu là buộc giảng viên phải sống với quyết định của người viết code.
    /// </summary>
    public bool AllowPartialCredit { get; set; }
}

public class QuestionOption : Entity
{
    public Guid QuestionId { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// Cấu hình chấm câu điền khuyết. Gap #12: chấm tiếng Việt không thể so sánh chuỗi thô.
/// </summary>
public class FillBlankConfig
{
    public List<string> AcceptedAnswers { get; set; } = [];
    public bool CaseSensitive { get; set; }
    public bool TrimWhitespace { get; set; } = true;

    /// <summary>
    /// Chuẩn hóa Unicode về NFC. Mặc định BẬT và gần như không bao giờ nên tắt: "tiếng Việt"
    /// gõ bằng Telex và gõ bằng bàn phím macOS ra hai chuỗi Unicode KHÁC NHAU nhưng nhìn
    /// giống hệt (tổ hợp ký tự + dấu, so với ký tự dựng sẵn). Không chuẩn hóa thì sinh viên
    /// trả lời đúng vẫn bị chấm sai, và không ai nhìn ra vì sao khi đọc bằng mắt.
    /// </summary>
    public bool NormalizeUnicode { get; set; } = true;

    public FillBlankConfig DeepCopy() => new()
    {
        AcceptedAnswers = [.. AcceptedAnswers],
        CaseSensitive = CaseSensitive,
        TrimWhitespace = TrimWhitespace,
        NormalizeUnicode = NormalizeUnicode,
    };
}
