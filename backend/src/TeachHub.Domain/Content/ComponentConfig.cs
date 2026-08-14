namespace TeachHub.Domain.Content;

/// <summary>
/// SRS khai config_json là jsonb tự do. Ở đây gom lại thành một kiểu có trường rõ ràng
/// để tránh magic string rải khắp code — vẫn lưu xuống DB dưới dạng jsonb qua ToJson().
/// Thêm loại component mới thì thêm trường ở đây, không phải migrate schema.
/// </summary>
public class ComponentConfig
{
    // recap
    public int? RecapQuestionCount { get; set; }
    public List<string> RecapFoundationTags { get; set; } = [];

    // hook / custom_note / code_along
    public string? Html { get; set; }

    // checkpoint_quiz (câu hỏi nhúng trực tiếp, không tạo Quiz riêng)
    public List<InlineQuestion> InlineQuestions { get; set; } = [];

    // reflection
    public string? ReflectionPrompt { get; set; }

    // code_along
    public string? StarterCodeUrl { get; set; }
    public string? Language { get; set; }

    public ComponentConfig DeepCopy() => new()
    {
        RecapQuestionCount = RecapQuestionCount,
        RecapFoundationTags = [.. RecapFoundationTags],
        Html = Html,
        InlineQuestions = [.. InlineQuestions.Select(q => q.DeepCopy())],
        ReflectionPrompt = ReflectionPrompt,
        StarterCodeUrl = StarterCodeUrl,
        Language = Language,
    };
}

public class InlineQuestion
{
    public required string Text { get; set; }
    public List<string> Options { get; set; } = [];
    public List<int> CorrectIndexes { get; set; } = [];
    public string? Explanation { get; set; }

    public InlineQuestion DeepCopy() => new()
    {
        Text = Text, Options = [.. Options],
        CorrectIndexes = [.. CorrectIndexes], Explanation = Explanation,
    };
}
